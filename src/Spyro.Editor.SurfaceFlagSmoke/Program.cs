using System.Buffers.Binary;
using Spyro.Editor.Core.Exporting;

const long LevelDataWadOffset = 0x100000;
const int CollisionStart = 0x20;
const int CollisionBody = CollisionStart + 4;
const int TreeStart = 0x40;
const int BlocksStart = 0x44;
const int TrianglesStart = 0x54;
const int OcclusionStart = 0xB4;
const int FlagsStart = 0xBC;
const int CollisionEnd = 0xC0;
const int UsedLevelEnd = 0xE0;

RunDirectFlagSmoke();
RunAlignmentSlotPromotionSmoke();
RunShiftAndCompositionSmoke();
RunConflictAndCapacityGuards();

Console.WriteLine("Native terrain surface flag promotion smoke passed.");

static void RunDirectFlagSmoke()
{
    byte[] source = BuildFixture();
    bool ok = NativeTerrainSurfaceFlagPromoter.TryBuild(
        source,
        LevelDataWadOffset,
        LevelDataWadOffset + CollisionStart,
        [new NativeTerrainSurfaceFlagAssignment(1, 2, "direct", "surface two")],
        Array.Empty<NativeTerrainSurfaceExistingPatch>(),
        out NativeTerrainSurfaceFlagPromotionPlan? plan,
        out string reason);
    Assert(ok && plan != null, $"Direct flag plan failed: {reason}");
    NativeTerrainSurfaceFlagPromotionPlan promotion = plan!;
    Assert(promotion.ShiftBytes == 0 && promotion.TriangleRemaps.Count == 0, "Direct flag assignment unexpectedly promoted collision storage.");
    Assert(promotion.WadOffset == LevelDataWadOffset + FlagsStart + 1, "Direct flag patch did not start at the selected native flag.");
    Assert(promotion.Before.Length == 1 && promotion.Before[0] == 0xC1 && promotion.After[0] == 0xC2, "Direct flag assignment did not preserve high native bits.");
}

static void RunAlignmentSlotPromotionSmoke()
{
    byte[] source = BuildFixture();
    bool ok = NativeTerrainSurfaceFlagPromoter.TryBuild(
        source,
        LevelDataWadOffset,
        LevelDataWadOffset + CollisionStart,
        [new NativeTerrainSurfaceFlagAssignment(6, 0, "alignment", "surface zero")],
        Array.Empty<NativeTerrainSurfaceExistingPatch>(),
        out NativeTerrainSurfaceFlagPromotionPlan? plan,
        out string reason);
    Assert(ok && plan != null, $"Alignment-slot plan failed: {reason}");
    NativeTerrainSurfaceFlagPromotionPlan promotion = plan!;
    Assert(promotion.OldFlagCount == 3 && promotion.NewFlagCount == 4 && promotion.ShiftBytes == 0, "The existing flag alignment byte was not used before shifting the suffix.");
    Assert(promotion.TriangleRemaps is [{ StagingTriangleIndex: 3, TargetTriangleIndex: 6 }], "Alignment-slot promotion did not remap triangle 6 through staging index 3.");

    byte[] final = ApplyPlan(source, promotion);
    Assert(ReadInt32(final, CollisionBody + 4) == 4, "Alignment-slot promotion did not update flagCount.");
    Assert(final[FlagsStart + 3] == 0xC0, "Promoted surface zero did not retain the implicit 0xC0 high bits.");
    Assert(TriangleBytes(final, 3).SequenceEqual(TriangleBytes(source, 6)), "Target geometry did not move into the promoted alignment slot.");
    Assert(TriangleBytes(final, 6).SequenceEqual(TriangleBytes(source, 3)), "Displaced staging geometry was not preserved.");
    Assert(ReadBlockWord(final, 3) == 6 && ReadBlockWord(final, 6) == 3, "Block-list references were not permuted with the triangle records.");
}

static void RunShiftAndCompositionSmoke()
{
    byte[] source = BuildFixture();
    int targetTriangleByte = TrianglesStart + (7 * 12);
    int suffixBackgroundByte = CollisionEnd + 4;
    NativeTerrainSurfaceExistingPatch[] existing =
    [
        new(
            LevelDataWadOffset + targetTriangleByte,
            [source[targetTriangleByte]],
            [0xE7],
            "existing-target-triangle-edit"),
        new(
            LevelDataWadOffset + suffixBackgroundByte,
            [source[suffixBackgroundByte]],
            [0x55],
            "existing-cyclorama-edit")
    ];
    bool ok = NativeTerrainSurfaceFlagPromoter.TryBuild(
        source,
        LevelDataWadOffset,
        LevelDataWadOffset + CollisionStart,
        [
            new NativeTerrainSurfaceFlagAssignment(4, 0, "direct-new", "surface zero"),
            new NativeTerrainSurfaceFlagAssignment(7, 1, "remapped", "surface one")
        ],
        existing,
        out NativeTerrainSurfaceFlagPromotionPlan? plan,
        out string reason);
    Assert(ok && plan != null, $"Suffix-shift plan failed: {reason}");
    NativeTerrainSurfaceFlagPromotionPlan promotion = plan!;
    Assert(promotion.OldFlagCount == 3 && promotion.NewFlagCount == 5, "Suffix-shift promotion chose the wrong flag count.");
    Assert(promotion.ShiftBytes == 4 && promotion.ZeroTailBytesBefore == 16 && promotion.ZeroTailBytesAfter == 12, "Suffix-shift promotion reported the wrong bounded capacity.");
    Assert(promotion.TriangleRemaps is [{ StagingTriangleIndex: 3, TargetTriangleIndex: 7 }], "The direct-new staging triangle was not excluded from the remap permutation.");
    Assert(promotion.ConsumedPatchWadOffsets.Order().SequenceEqual(existing.Select(patch => patch.WadOffset).Order()), "Existing collision/suffix patches were not composed atomically.");

    byte[] final = ApplyPlan(source, promotion);
    Assert(ReadInt32(final, CollisionStart) == ReadInt32(source, CollisionStart) + 4, "Collision component length did not grow by the suffix shift.");
    Assert(ReadInt32(final, CollisionBody + 4) == 5, "Suffix-shift promotion did not update flagCount.");
    Assert(ReadInt32(final, CollisionBody + 0x08) == ReadInt32(source, CollisionBody + 0x08) &&
           ReadInt32(final, CollisionBody + 0x0C) == ReadInt32(source, CollisionBody + 0x0C) &&
           ReadInt32(final, CollisionBody + 0x10) == ReadInt32(source, CollisionBody + 0x10) &&
           ReadInt32(final, CollisionBody + 0x14) == ReadInt32(source, CollisionBody + 0x14) &&
           ReadInt32(final, CollisionBody + 0x18) == ReadInt32(source, CollisionBody + 0x18),
        "Collision table pointers changed during suffix growth.");
    Assert(final[FlagsStart + 3] == 0xC1 && final[FlagsStart + 4] == 0xC0, "New explicit flags did not preserve implicit high bits and requested surface indexes.");
    Assert(final[targetTriangleByte - (4 * 12)] == 0xE7, "An existing target-triangle edit did not travel with the remapped geometry.");
    Assert(final[suffixBackgroundByte + 4] == 0x55, "An existing cyclorama edit did not travel with the shifted suffix.");

    byte[] expectedSuffix = source.AsSpan(CollisionEnd, UsedLevelEnd - CollisionEnd).ToArray();
    expectedSuffix[4] = 0x55;
    Assert(final.AsSpan(CollisionEnd + 4, expectedSuffix.Length).SequenceEqual(expectedSuffix), "The parsed suffix was not preserved byte-for-byte after composition.");
    Assert(final.AsSpan(UsedLevelEnd + 4).ToArray().All(value => value == 0), "The shortened level-data tail is not all zero.");
}

static void RunConflictAndCapacityGuards()
{
    byte[] source = BuildFixture();
    NativeTerrainSurfaceExistingPatch paddingConflict = new(
        LevelDataWadOffset + UsedLevelEnd,
        [source[UsedLevelEnd]],
        [0x66],
        "padding-owner");
    bool conflictOk = NativeTerrainSurfaceFlagPromoter.TryBuild(
        source,
        LevelDataWadOffset,
        LevelDataWadOffset + CollisionStart,
        [
            new NativeTerrainSurfaceFlagAssignment(6, 0, "a", "surface zero"),
            new NativeTerrainSurfaceFlagAssignment(7, 1, "b", "surface one")
        ],
        [paddingConflict],
        out _,
        out string conflictReason);
    Assert(!conflictOk && conflictReason.Contains("padding", StringComparison.OrdinalIgnoreCase), "A patch in padding consumed by suffix growth was not blocked.");

    bool duplicateOk = NativeTerrainSurfaceFlagPromoter.TryBuild(
        source,
        LevelDataWadOffset,
        LevelDataWadOffset + CollisionStart,
        [
            new NativeTerrainSurfaceFlagAssignment(6, 0, "a", "surface zero"),
            new NativeTerrainSurfaceFlagAssignment(6, 1, "b", "surface one")
        ],
        Array.Empty<NativeTerrainSurfaceExistingPatch>(),
        out _,
        out string duplicateReason);
    Assert(!duplicateOk && duplicateReason.Contains("both", StringComparison.OrdinalIgnoreCase), "Conflicting assignments to one collision triangle were not blocked.");

    byte[] noTail = source.AsSpan(0, UsedLevelEnd).ToArray();
    bool capacityOk = NativeTerrainSurfaceFlagPromoter.TryBuild(
        noTail,
        LevelDataWadOffset,
        LevelDataWadOffset + CollisionStart,
        [
            new NativeTerrainSurfaceFlagAssignment(6, 0, "a", "surface zero"),
            new NativeTerrainSurfaceFlagAssignment(7, 1, "b", "surface one")
        ],
        Array.Empty<NativeTerrainSurfaceExistingPatch>(),
        out _,
        out string capacityReason);
    Assert(!capacityOk && capacityReason.Contains("only 0", StringComparison.OrdinalIgnoreCase), "Insufficient zero-tail capacity was not blocked.");
}

static byte[] BuildFixture()
{
    byte[] bytes = new byte[UsedLevelEnd + 16];
    WriteInt32(bytes, CollisionStart, CollisionEnd - CollisionStart);
    WriteInt32(bytes, CollisionBody, 8);
    WriteInt32(bytes, CollisionBody + 4, 3);
    WriteInt32(bytes, CollisionBody + 0x08, TreeStart - CollisionBody);
    WriteInt32(bytes, CollisionBody + 0x0C, BlocksStart - CollisionBody);
    WriteInt32(bytes, CollisionBody + 0x10, TrianglesStart - CollisionBody);
    WriteInt32(bytes, CollisionBody + 0x14, OcclusionStart - CollisionBody);
    WriteInt32(bytes, CollisionBody + 0x18, FlagsStart - CollisionBody);
    for (int index = 0; index < 8; index++)
    {
        WriteUInt16(bytes, BlocksStart + (index * 2), (ushort)(index == 0 ? 0x8000 : index));
        for (int part = 0; part < 12; part++)
            bytes[TrianglesStart + (index * 12) + part] = (byte)(0x10 + index + part);
        bytes[OcclusionStart + index] = (byte)(0x80 + index);
    }
    bytes[FlagsStart] = 0x3F;
    bytes[FlagsStart + 1] = 0xC1;
    bytes[FlagsStart + 2] = 0x3F;
    bytes[FlagsStart + 3] = 0;

    WriteInt32(bytes, CollisionEnd, 12);
    bytes[CollisionEnd + 4] = 0x11;
    bytes[CollisionEnd + 5] = 0x22;
    bytes[CollisionEnd + 6] = 0x33;
    WriteInt32(bytes, CollisionEnd + 8, 0);
    WriteInt32(bytes, CollisionEnd + 12, 0);
    WriteInt32(bytes, CollisionEnd + 16, 8);
    WriteInt32(bytes, CollisionEnd + 20, 0);
    WriteInt32(bytes, CollisionEnd + 24, 8);
    WriteInt32(bytes, CollisionEnd + 28, 0);
    return bytes;
}

static byte[] ApplyPlan(byte[] source, NativeTerrainSurfaceFlagPromotionPlan plan)
{
    byte[] final = source.ToArray();
    int relative = checked((int)(plan.WadOffset - LevelDataWadOffset));
    plan.After.CopyTo(final, relative);
    return final;
}

static byte[] TriangleBytes(byte[] bytes, int index) =>
    bytes.AsSpan(TrianglesStart + (index * 12), 12).ToArray();

static int ReadBlockWord(byte[] bytes, int index) =>
    BinaryPrimitives.ReadUInt16LittleEndian(bytes.AsSpan(BlocksStart + (index * 2), 2)) & 0x7FFF;

static int ReadInt32(byte[] bytes, int offset) =>
    BinaryPrimitives.ReadInt32LittleEndian(bytes.AsSpan(offset, 4));

static void WriteInt32(byte[] bytes, int offset, int value) =>
    BinaryPrimitives.WriteInt32LittleEndian(bytes.AsSpan(offset, 4), value);

static void WriteUInt16(byte[] bytes, int offset, ushort value) =>
    BinaryPrimitives.WriteUInt16LittleEndian(bytes.AsSpan(offset, 2), value);

static void Assert(bool condition, string message)
{
    if (!condition)
        throw new InvalidOperationException(message);
}
