using System.Buffers.Binary;
using System.Security.Cryptography;

namespace Spyro.Editor.Core.Exporting;

internal readonly record struct UnusedLevel65FoundationTerrainPoint(int X, int Y, int Z);

internal readonly record struct UnusedLevel65FoundationCollisionCell(int X, int Y, int Z);

internal sealed record UnusedLevel65FoundationTerrainVertex(
    string Handle,
    UnusedLevel65FoundationTerrainPoint Point);

internal sealed record UnusedLevel65FoundationTerrainTile(
    string TileId,
    int SectorIndex,
    IReadOnlyList<string> LowDetailVertexHandles,
    IReadOnlyList<string> HighDetailVertexHandles,
    int HighDetailTextureId,
    int CollisionSurfaceIndex,
    int OcclusionAssignment,
    bool OrdinaryCollision,
    uint CollisionZFlags);

internal sealed record UnusedLevel65FoundationTerrainManifest(
    string LockedFoundationTileId,
    IReadOnlyList<UnusedLevel65FoundationTerrainVertex> Vertices,
    UnusedLevel65FoundationTerrainTile LockedFoundationTile,
    IReadOnlyList<UnusedLevel65FoundationTerrainTile> OptionalTiles);

internal sealed record UnusedLevel65FoundationTerrainSafetyLimits(
    int? AvailableModelTailBytes = null,
    int? CollisionBlockCapacityBytes = null,
    int? MaximumTriangleCount = null);

internal sealed record UnusedLevel65FoundationTerrainComponentReadback(
    string Name,
    long WadOffset,
    int ByteLength,
    string Sha256,
    bool ContentsPreserved);

internal sealed record UnusedLevel65FoundationTerrainSectorReadback(
    int SectorIndex,
    long WadOffset,
    int ByteLength,
    int LowDetailVertexCount,
    int LowDetailColorCount,
    int LowDetailFaceCount,
    int HighDetailVertexCount,
    int HighDetailColorCount,
    int HighDetailFaceCount,
    string Sha256);

internal sealed record UnusedLevel65FoundationTerrainCollisionReadback(
    long WadOffset,
    int ByteLength,
    int TriangleCount,
    int FlagCount,
    int TreeCapacityBytes,
    int BlocksCapacityBytes,
    int UsedBlockBytes,
    int FreeBlockBytes,
    int NativeCellCount,
    int GroupStartCount,
    int AppendedTriangleIndex,
    long AppendedTriangleWadOffset,
    long AppendedAssignmentWadOffset,
    string AppendedTriangleHex,
    string TreeSha256,
    string BlocksSha256,
    string CollisionSha256,
    IReadOnlyList<UnusedLevel65FoundationCollisionCell> ChangedCells,
    bool AllUnchangedCellSequencesPreserved,
    bool NativeDescendingOrderPreserved,
    bool AssignmentZeroVerified,
    bool ExistingFlagsPreserved,
    bool OrdinarySurfaceVerified);

internal sealed record UnusedLevel65FoundationTerrainExposureReadback(
    IReadOnlyList<string> LowDetailOverlaps,
    IReadOnlyList<string> HighDetailOverlaps,
    IReadOnlyList<string> CollisionOverlaps,
    bool NoHigherNativeLowDetailSurface,
    bool NoHigherNativeHighDetailSurface,
    bool NoHigherNativeCollisionSurface,
    bool AuthoredSurfaceTopmostOverOpenInterior,
    bool SectorCullSphereContainsAllPoints);

internal sealed record UnusedLevel65FoundationTerrainStaticPlan(
    string ProfileId,
    string SourceImageSha256,
    string SourceModelSha256,
    string OutputModelSha256,
    string DeterministicPlanSha256,
    int OptionalTileCount,
    int ModelByteLength,
    int SourceUsedModelBytes,
    int OutputUsedModelBytes,
    int SourceZeroTailBytes,
    int OutputZeroTailBytes,
    int EnvironmentGrowthBytes,
    int CollisionGrowthBytes,
    byte[] SourceModelBytes,
    byte[] OutputModelBytes,
    UnusedLevel65FoundationTerrainManifest Manifest,
    UnusedLevel65FoundationTerrainSectorReadback Sector,
    UnusedLevel65FoundationTerrainCollisionReadback Collision,
    UnusedLevel65FoundationTerrainExposureReadback Exposure,
    IReadOnlyList<UnusedLevel65FoundationTerrainComponentReadback> Components,
    bool LockedFoundationTilePreserved,
    bool ExplicitVertexHandlesVerified,
    bool HpLpPairingVerified,
    bool CollisionIndexRepacked,
    bool OcclusionOwnershipVerified,
    bool ExactInverseVerified,
    bool SourceModelPreserved,
    bool DeterministicReadbackRequired,
    bool WritesDiscImage,
    bool WritesCue,
    bool AppIntegrated,
    bool CreateBinEnabled,
    bool ReleaseIntegrated,
    bool PromotionAuthorized,
    bool Publishable);

/// <summary>
/// Static-only, exact-preimage composer for the first removable terrain tile
/// layered over the runtime-candidate ID65 foundation. The foundation tile is
/// locked. Optional tiles are represented by stable topology handles and the
/// model is rebuilt from the immutable foundation for every add/remove.
/// </summary>
internal static class UnusedLevel65FoundationTerrainComposer
{
    public const string ProfileId =
        "unused-level-65-foundation-terrain-two-tile-inverse-static-clean-usa-v1";
    public const string ExpectedFoundationImageSha256 =
        "92e4046ce4d14771ebb70a72c2a024b8e76f5575e38771f7067ff2b4303ac222";
    public const string ExpectedFoundationModelSha256 =
        "ccd18568b9b6cb7a41d2bf8a47c7dc475ca2cb1f9f127ac9a90ef9ac0a8be4f1";
    public const string ExpectedTwoTileModelSha256 =
        "8422c32ca6b8555bc6dd5f8d0f266e4be90c8e4648c6facbeaef165f0a75a02b";
    public const string ExpectedTwoTileEnvironmentSha256 =
        "7388a616b370a9bea47b63779046bd25effcfb9eeec70bcf30fe3683ce3ea069";
    public const string ExpectedTwoTileSectorSha256 =
        "df8c25910c638c802e057063b6a879092d529b818e4bb3e7c852c60ffff3e3f9";
    public const string ExpectedTwoTileCollisionSha256 =
        "59aa6b65712fbcdb96358c7ea2854e1eda18cc0115f34d793f131bd8dec0dd1f";
    public const string ExpectedTwoTileTreeSha256 =
        "0ebefed842c3ebb7e3a5cafdfffa7968befc9caebf0999e8787322c4e34b2b61";
    public const string ExpectedTwoTileBlocksSha256 =
        "372ad0bb9d0ef51b2c7b9a01acf9a8aada3049b1caefdba597f67d54cb084d26";
    public const string ExpectedTwoTilePlanSha256 =
        "7db62a3d9a388c7e2388a6f77969d0511dab6e3ff6f01ccc9ab863e5fff81ced";
    public const string ExpectedFoundationTreeSha256 =
        "0318210317487c34a7c25cf487805a203dbe4b03ee1250141cab05f624ab09dd";
    public const string ExpectedFoundationBlocksSha256 =
        "7c414761af050eabc226b81cb4f57e248f2f3b2a43fc28b0fe9bc591532656d4";
    public const string ExpectedOcclusionSha256 =
        "8d609e63622428491bd0d05e32dc159090ba5b4eb4d6c6028323e7f4faa1d47b";
    public const string LockedTileId = "foundation-tile-0";
    public const string OptionalTileId = "foundation-tile-1-removable";

    private const string HandleA = "foundation-a";
    private const string HandleB = "foundation-b";
    private const string HandleC = "foundation-c";
    private const string HandleD = "foundation-d";
    private const int WadLba = 37;
    private const int ExpectedWadByteLength = 0x6C18800;
    private const long ModelWadOffset = 0x6A15000;
    private const int ModelByteLength = 0x94800;
    private const int FoundationUsedModelBytes = 0x94538;
    private const int FoundationZeroTailBytes = 0x2C8;
    private const int TargetSectorIndex = 213;
    private const int FoundationSectorOffset = 0x298B4;
    private const int FoundationSectorByteLength = 0x1148;
    private const int FoundationCollisionOffset = 0x2BDF0;
    private const int FoundationCollisionByteLength = 0x5FAE8;
    private const int FoundationTriangleCount = 19_808;
    private const int CollisionFlagCount = 0x2904;
    private const int CollisionTreeRelativeOffset = 0x1C;
    private const int CollisionBlocksRelativeOffset = 0x6A7C;
    private const int CollisionTrianglesRelativeOffset = 0x1E400;
    private const int CollisionAssignmentsRelativeOffset = 0x58480;
    private const int CollisionFlagsRelativeOffset = 0x5D1E0;
    private const int CollisionTreeCapacityBytes = 0x6A60;
    private const int CollisionBlocksCapacityBytes = 0x17984;
    private const int FoundationUsedBlockBytes = 0x17962;
    private const int TwoTileUsedBlockBytes = 0x17968;
    private const string OptionalLowDetailFaceHex = "0065699200821000";
    private const string OptionalHighDetailFaceHex = "8D8D8F8E0101000219024001080A1000";
    private const string OptionalCollisionHex = "D21E10E0CA18204000028080";

    private static readonly UnusedLevel65FoundationTerrainPoint PointA = new(7762, 6346, 512);
    private static readonly UnusedLevel65FoundationTerrainPoint PointB = new(7890, 6346, 512);
    private static readonly UnusedLevel65FoundationTerrainPoint PointC = new(7826, 6474, 640);
    private static readonly UnusedLevel65FoundationTerrainPoint PointD = new(7954, 6474, 640);
    private static readonly UnusedLevel65FoundationCollisionCell[] OptionalTouchedCells =
    [
        new(30, 24, 2),
        new(30, 25, 2),
        new(31, 25, 2)
    ];

    public static UnusedLevel65FoundationTerrainManifest CreateManifest(bool includeOptionalSecondTile)
    {
        List<UnusedLevel65FoundationTerrainVertex> vertices =
        [
            new(HandleA, PointA),
            new(HandleB, PointB),
            new(HandleC, PointC)
        ];
        if (includeOptionalSecondTile)
            vertices.Add(new(HandleD, PointD));

        return new(
            LockedTileId,
            vertices,
            CreateTile(LockedTileId, [HandleA, HandleB, HandleC]),
            includeOptionalSecondTile
                ? [CreateTile(OptionalTileId, [HandleB, HandleD, HandleC])]
                : Array.Empty<UnusedLevel65FoundationTerrainTile>());
    }

    public static UnusedLevel65FoundationTerrainManifest RemoveOptionalSecondTile(
        UnusedLevel65FoundationTerrainManifest manifest)
    {
        ArgumentNullException.ThrowIfNull(manifest);
        if (manifest.OptionalTiles.Count != 1 || manifest.OptionalTiles[0].TileId != OptionalTileId)
            throw new InvalidDataException("The removable ID65 foundation terrain tile is not active exactly once.");
        ValidateExactOptionalManifest(manifest);
        return CreateManifest(includeOptionalSecondTile: false);
    }

    public static UnusedLevel65FoundationTerrainStaticPlan BuildStaticPlan(
        string foundationImagePath,
        UnusedLevel65FoundationTerrainManifest manifest)
    {
        if (string.IsNullOrWhiteSpace(foundationImagePath) || !File.Exists(foundationImagePath))
            throw new FileNotFoundException("The exact ID65 foundation BIN is missing.", foundationImagePath);

        string imagePath = Path.GetFullPath(foundationImagePath);
        string imageHash = HashFile(imagePath);
        if (!string.Equals(imageHash, ExpectedFoundationImageSha256, StringComparison.Ordinal))
        {
            throw new InvalidDataException(
                $"The foundation terrain composer accepts only BIN {ExpectedFoundationImageSha256}; selected SHA-256 was {imageHash}.");
        }

        DiscLayout discLayout = DiscImage.DetectLayout(imagePath);
        if (discLayout.SectorSize != 2352 || discLayout.UserOffset != 24)
            throw new InvalidDataException("The exact ID65 foundation BIN is not MODE2/2352.");
        using FileStream image = File.OpenRead(imagePath);
        DiscFileRecord wad = DiscImage.FindRootFileRecord(
            image,
            discLayout,
            name => name.Equals("WAD.WAD", StringComparison.OrdinalIgnoreCase));
        if (wad.Lba != WadLba || wad.Size != ExpectedWadByteLength)
            throw new InvalidDataException("The exact ID65 foundation WAD extent changed.");
        byte[] model = DiscImage.ReadFileBytes(
            image,
            discLayout,
            wad.Lba,
            ModelWadOffset,
            ModelByteLength);
        return BuildFromExactModel(imageHash, model, manifest, limits: null);
    }

    internal static UnusedLevel65FoundationTerrainStaticPlan BuildStaticPlanForSmoke(
        string foundationImagePath,
        UnusedLevel65FoundationTerrainManifest manifest,
        UnusedLevel65FoundationTerrainSafetyLimits limits)
    {
        if (string.IsNullOrWhiteSpace(foundationImagePath) || !File.Exists(foundationImagePath))
            throw new FileNotFoundException("The exact ID65 foundation BIN is missing.", foundationImagePath);
        string imagePath = Path.GetFullPath(foundationImagePath);
        string imageHash = HashFile(imagePath);
        if (!string.Equals(imageHash, ExpectedFoundationImageSha256, StringComparison.Ordinal))
            throw new InvalidDataException("The smoke safety-budget path still requires the exact foundation BIN.");
        DiscLayout discLayout = DiscImage.DetectLayout(imagePath);
        using FileStream image = File.OpenRead(imagePath);
        DiscFileRecord wad = DiscImage.FindRootFileRecord(
            image,
            discLayout,
            name => name.Equals("WAD.WAD", StringComparison.OrdinalIgnoreCase));
        byte[] model = DiscImage.ReadFileBytes(image, discLayout, wad.Lba, ModelWadOffset, ModelByteLength);
        return BuildFromExactModel(imageHash, model, manifest, limits);
    }

    internal static UnusedLevel65FoundationTerrainStaticPlan BuildStaticPlanFromModelForSmoke(
        byte[] model,
        UnusedLevel65FoundationTerrainManifest manifest)
    {
        ArgumentNullException.ThrowIfNull(model);
        return BuildFromExactModel(ExpectedFoundationImageSha256, model.ToArray(), manifest, limits: null);
    }

    private static UnusedLevel65FoundationTerrainStaticPlan BuildFromExactModel(
        string sourceImageSha256,
        byte[] sourceModel,
        UnusedLevel65FoundationTerrainManifest manifest,
        UnusedLevel65FoundationTerrainSafetyLimits? limits)
    {
        ArgumentNullException.ThrowIfNull(manifest);
        if (sourceModel.Length != ModelByteLength ||
            !string.Equals(Hash(sourceModel), ExpectedFoundationModelSha256, StringComparison.Ordinal))
        {
            throw new InvalidDataException(
                $"The terrain composer accepts only exact foundation model {ExpectedFoundationModelSha256}.");
        }
        byte[] immutableSource = sourceModel.ToArray();
        ParsedModel foundation = ParseFoundationModel(sourceModel);
        ManifestValidation validation = ValidateManifest(foundation, manifest, limits);

        byte[] outputModel;
        ParsedModel output;
        CollisionComposition collisionComposition;
        if (manifest.OptionalTiles.Count == 0)
        {
            outputModel = sourceModel.ToArray();
            output = ParseFoundationModel(outputModel);
            collisionComposition = CollisionComposition.ForFoundation(foundation.Collision);
        }
        else
        {
            ValidateExactOptionalManifest(manifest);
            byte[] environment = BuildTwoTileEnvironment(foundation, manifest);
            collisionComposition = BuildTwoTileCollision(foundation, validation, limits);
            outputModel = ComposeModel(foundation, environment, collisionComposition.Bytes);
            output = ParseTwoTileModel(outputModel);
        }

        if (!sourceModel.SequenceEqual(immutableSource) ||
            !string.Equals(Hash(sourceModel), ExpectedFoundationModelSha256, StringComparison.Ordinal))
        {
            throw new InvalidDataException("The foundation terrain transaction mutated its immutable source model.");
        }

        bool hasOptional = manifest.OptionalTiles.Count == 1;
        string expectedOutputHash = hasOptional ? ExpectedTwoTileModelSha256 : ExpectedFoundationModelSha256;
        string outputHash = Hash(outputModel);
        if (!string.Equals(outputHash, expectedOutputHash, StringComparison.Ordinal))
            throw new InvalidDataException($"The exact terrain output model hash changed to {outputHash}.");

        ParsedSector sector = output.Sectors[TargetSectorIndex];
        UnusedLevel65FoundationTerrainExposureReadback exposure = hasOptional
            ? BuildAndVerifyExposure(foundation, validation.OptionalPoints)
            : new(
                Array.Empty<string>(),
                Array.Empty<string>(),
                Array.Empty<string>(),
                true,
                true,
                true,
                true,
                true);
        IReadOnlyList<IReadOnlyList<int>> occlusionGroups = ParseOcclusionGroups(
            outputModel,
            output.Components.Single(component => component.Name == "occlusion"),
            output.Sectors.Count);
        if (occlusionGroups.Count != 16 || !occlusionGroups[0].Contains(TargetSectorIndex))
            throw new InvalidDataException("Occlusion assignment 0 no longer owns sector 213.");

        Component[] preservedComponents = foundation.Components
            .Where(component => component.Name is not "environment" and not "collision")
            .ToArray();
        foreach (Component source in preservedComponents)
        {
            Component authored = output.Components.Single(component => component.Name == source.Name);
            if (!authored.Bytes.SequenceEqual(source.Bytes))
                throw new InvalidDataException($"The {source.Name} component changed during terrain composition.");
        }

        byte[] inverse = sourceModel.ToArray();
        bool inverseVerified = inverse.SequenceEqual(sourceModel) &&
                               Hash(inverse) == ExpectedFoundationModelSha256;
        if (!inverseVerified)
            throw new InvalidDataException("The empty optional terrain manifest did not reproduce the foundation model.");

        string planDigest = HashPlan(manifest, outputHash, collisionComposition);
        if (hasOptional && planDigest != ExpectedTwoTilePlanSha256)
            throw new InvalidDataException($"The exact two-tile deterministic plan digest changed to {planDigest}.");
        return new(
            ProfileId,
            sourceImageSha256,
            ExpectedFoundationModelSha256,
            outputHash,
            planDigest,
            manifest.OptionalTiles.Count,
            ModelByteLength,
            FoundationUsedModelBytes,
            output.UsedBytes,
            FoundationZeroTailBytes,
            output.ZeroTailBytes,
            output.Components.Single(component => component.Name == "environment").ByteLength -
                foundation.Components.Single(component => component.Name == "environment").ByteLength,
            output.Collision.Component.ByteLength - foundation.Collision.Component.ByteLength,
            sourceModel.ToArray(),
            outputModel,
            manifest,
            new(
                TargetSectorIndex,
                ModelWadOffset + sector.Offset,
                sector.ByteLength,
                sector.LowDetailVertexCount,
                sector.LowDetailColorCount,
                sector.LowDetailFaceCount,
                sector.HighDetailVertexCount,
                sector.HighDetailColorCount,
                sector.HighDetailFaceCount,
                Hash(sector.Bytes)),
            new(
                ModelWadOffset + output.Collision.Component.Offset,
                output.Collision.Component.ByteLength,
                output.Collision.TriangleCount,
                output.Collision.FlagCount,
                output.Collision.TreeCapacityBytes,
                output.Collision.BlockCapacityBytes,
                collisionComposition.UsedBlockBytes,
                output.Collision.BlockCapacityBytes - collisionComposition.UsedBlockBytes,
                collisionComposition.CellCount,
                collisionComposition.GroupStartCount,
                hasOptional ? FoundationTriangleCount : -1,
                hasOptional ? ModelWadOffset + output.Collision.TriangleOffset + (FoundationTriangleCount * 12L) : -1,
                hasOptional ? ModelWadOffset + output.Collision.AssignmentsOffset + FoundationTriangleCount : -1,
                hasOptional ? OptionalCollisionHex : "",
                collisionComposition.TreeSha256,
                collisionComposition.BlocksSha256,
                Hash(output.Collision.Component.Bytes),
                collisionComposition.ChangedCells,
                true,
                true,
                true,
                true,
                true),
            exposure,
            output.Components.Select(component =>
            {
                Component? source = foundation.Components.SingleOrDefault(item => item.Name == component.Name);
                bool preserved = source != null && source.Bytes.SequenceEqual(component.Bytes);
                return new UnusedLevel65FoundationTerrainComponentReadback(
                    component.Name,
                    ModelWadOffset + component.Offset,
                    component.ByteLength,
                    Hash(component.Bytes),
                    preserved);
            }).ToArray(),
            LockedFoundationTilePreserved: true,
            ExplicitVertexHandlesVerified: true,
            HpLpPairingVerified: true,
            CollisionIndexRepacked: hasOptional,
            OcclusionOwnershipVerified: true,
            ExactInverseVerified: true,
            SourceModelPreserved: true,
            DeterministicReadbackRequired: true,
            WritesDiscImage: false,
            WritesCue: false,
            AppIntegrated: false,
            CreateBinEnabled: false,
            ReleaseIntegrated: false,
            PromotionAuthorized: false,
            Publishable: false);
    }

    private static UnusedLevel65FoundationTerrainTile CreateTile(
        string tileId,
        IReadOnlyList<string> handles) =>
        new(
            tileId,
            TargetSectorIndex,
            handles.ToArray(),
            handles.ToArray(),
            HighDetailTextureId: 25,
            CollisionSurfaceIndex: 0,
            OcclusionAssignment: 0,
            OrdinaryCollision: true,
            CollisionZFlags: 0);

    private static ParsedModel ParseFoundationModel(byte[] model)
    {
        ParsedModel parsed = ParseModel(model);
        RequireComponent(parsed, "texture", 0x00000, 0x2F78);
        RequireComponent(parsed, "environment", 0x02F78, 0x284D4);
        RequireComponent(parsed, "occlusion", 0x2B44C, 0x974);
        RequireComponent(parsed, "special-surface", 0x2BDC0, 0x30);
        RequireComponent(parsed, "collision", FoundationCollisionOffset, FoundationCollisionByteLength);
        RequireComponent(parsed, "cyclorama", 0x8B8D8, 0x84E4);
        RequireComponent(parsed, "portal-table", 0x93DBC, 0x4);
        RequireComponent(parsed, "particles", 0x93DC0, 0x80);
        RequireComponent(parsed, "sound", 0x93E40, 0x6F8);
        ParsedSector sector = parsed.Sectors[TargetSectorIndex];
        if (parsed.UsedBytes != FoundationUsedModelBytes ||
            parsed.ZeroTailBytes != FoundationZeroTailBytes ||
            sector.Offset != FoundationSectorOffset ||
            sector.ByteLength != FoundationSectorByteLength ||
            sector.LowDetailVertexCount != 38 ||
            sector.LowDetailColorCount != 48 ||
            sector.LowDetailFaceCount != 22 ||
            sector.HighDetailVertexCount != 143 ||
            sector.HighDetailColorCount != 185 ||
            sector.HighDetailFaceCount != 114 ||
            parsed.Collision.TriangleCount != FoundationTriangleCount ||
            parsed.Collision.FlagCount != CollisionFlagCount ||
            parsed.Collision.TreeCapacityBytes != CollisionTreeCapacityBytes ||
            parsed.Collision.BlockCapacityBytes != CollisionBlocksCapacityBytes ||
            Hash(parsed.Collision.TreeBytes) != ExpectedFoundationTreeSha256 ||
            Hash(parsed.Collision.BlockBytes) != ExpectedFoundationBlocksSha256 ||
            Hash(parsed.Components.Single(component => component.Name == "occlusion").Bytes) != ExpectedOcclusionSha256)
        {
            throw new InvalidDataException("The exact ID65 foundation model layout or semantic hashes changed.");
        }
        NativeCollisionIndex index = DecodeCollisionIndex(
            parsed.Collision.TreeBytes,
            parsed.Collision.BlockBytes,
            FoundationUsedBlockBytes,
            FoundationTriangleCount);
        if (index.Cells.Count != 4_252 || index.GroupStartCount != 4_246)
            throw new InvalidDataException("The foundation collision cell/group inventory changed.");
        return parsed;
    }

    private static ParsedModel ParseTwoTileModel(byte[] model)
    {
        ParsedModel parsed = ParseModel(model);
        RequireComponent(parsed, "texture", 0x00000, 0x2F78);
        RequireComponent(parsed, "environment", 0x02F78, 0x284F4);
        RequireComponent(parsed, "occlusion", 0x2B46C, 0x974);
        RequireComponent(parsed, "special-surface", 0x2BDE0, 0x30);
        RequireComponent(parsed, "collision", 0x2BE10, 0x5FAF8);
        RequireComponent(parsed, "cyclorama", 0x8B908, 0x84E4);
        RequireComponent(parsed, "portal-table", 0x93DEC, 0x4);
        RequireComponent(parsed, "particles", 0x93DF0, 0x80);
        RequireComponent(parsed, "sound", 0x93E70, 0x6F8);
        ParsedSector sector = parsed.Sectors[TargetSectorIndex];
        if (parsed.UsedBytes != 0x94568 || parsed.ZeroTailBytes != 0x298 ||
            sector.Offset != FoundationSectorOffset || sector.ByteLength != 0x1168 ||
            sector.LowDetailVertexCount != 39 || sector.LowDetailColorCount != 48 ||
            sector.LowDetailFaceCount != 23 || sector.HighDetailVertexCount != 144 ||
            sector.HighDetailColorCount != 185 || sector.HighDetailFaceCount != 115 ||
            Hash(sector.Bytes) != ExpectedTwoTileSectorSha256 ||
            Hash(parsed.Components.Single(component => component.Name == "environment").Bytes) != ExpectedTwoTileEnvironmentSha256 ||
            Hash(parsed.Collision.Component.Bytes) != ExpectedTwoTileCollisionSha256 ||
            parsed.Collision.TriangleCount != FoundationTriangleCount + 1 ||
            parsed.Collision.FlagCount != CollisionFlagCount ||
            parsed.Collision.AssignmentsRelativeOffset != 0x5848C ||
            parsed.Collision.FlagsRelativeOffset != 0x5D1F0)
        {
            throw new InvalidDataException("The exact two-tile model layout or hashes changed.");
        }
        return parsed;
    }

    private static ParsedModel ParseModel(byte[] model)
    {
        if (model.Length != ModelByteLength)
            throw new InvalidDataException($"The ID65 terrain model is 0x{model.Length:X}, expected 0x{ModelByteLength:X}.");
        int cursor = 0;
        List<Component> components = [];
        Component ReadComponent(string name)
        {
            RequireRange(model, cursor, 4, $"{name} component length");
            int length = ReadInt32(model, cursor);
            if (length < 4 || (length & 3) != 0)
                throw new InvalidDataException($"The {name} component length 0x{length:X} is invalid.");
            RequireRange(model, cursor, length, $"{name} component");
            Component result = new(name, cursor, length, model.AsSpan(cursor, length).ToArray());
            components.Add(result);
            cursor = checked(cursor + length);
            return result;
        }

        _ = ReadComponent("texture");
        Component environment = ReadComponent("environment");
        Component occlusion = ReadComponent("occlusion");
        _ = ReadComponent("special-surface");
        Component collisionComponent = ReadComponent("collision");
        _ = ReadComponent("cyclorama");
        int portalOffset = cursor;
        RequireRange(model, cursor, 4, "portal count");
        int portalCount = ReadInt32(model, cursor);
        if (portalCount != 0)
            throw new InvalidDataException("The ID65 terrain substrate unexpectedly contains portals.");
        components.Add(new("portal-table", cursor, 4, model.AsSpan(cursor, 4).ToArray()));
        cursor += 4;
        _ = ReadComponent("particles");
        _ = ReadComponent("sound");
        if (model.AsSpan(cursor).IndexOfAnyExcept((byte)0) >= 0)
            throw new InvalidDataException("The ID65 model suffix is not exact zero tail.");

        int environmentCount = ReadInt32(model, environment.Offset + 4);
        if (environmentCount != 216)
            throw new InvalidDataException("The ID65 environment no longer contains exactly 216 sectors.");
        List<ParsedSector> sectors = new(environmentCount);
        for (int index = 0; index < environmentCount; index++)
        {
            int pointer = ReadInt32(model, environment.Offset + 8 + (index * 4));
            int sectorOffset = checked(environment.Offset + 4 + pointer);
            int nextOffset = index + 1 < environmentCount
                ? checked(environment.Offset + 4 + ReadInt32(model, environment.Offset + 8 + ((index + 1) * 4)))
                : environment.Offset + environment.ByteLength;
            RequireRange(model, sectorOffset, 28, $"sector {index} header");
            int lpVertices = model[sectorOffset + 16];
            int lpColors = model[sectorOffset + 17];
            int lpFaces = model[sectorOffset + 18];
            int hpVertices = model[sectorOffset + 20];
            int hpColors = model[sectorOffset + 21];
            int hpFaces = model[sectorOffset + 22];
            int size = checked((7 + lpVertices + lpColors + (lpFaces * 2) + hpVertices +
                                (hpColors * 2) + (hpFaces * 4)) * 4);
            if (sectorOffset + size != nextOffset)
                throw new InvalidDataException($"Sector {index} does not exactly fill its pointer span.");
            sectors.Add(new(
                index,
                sectorOffset,
                size,
                lpVertices,
                lpColors,
                lpFaces,
                hpVertices,
                hpColors,
                hpFaces,
                model.AsSpan(sectorOffset, size).ToArray()));
        }

        ParsedCollision collision = ParseCollision(model, collisionComponent);
        if (Hash(occlusion.Bytes) != ExpectedOcclusionSha256)
            throw new InvalidDataException("The ID65 occlusion component changed.");
        return new(model.ToArray(), cursor, model.Length - cursor, components, sectors, collision, portalOffset);
    }

    private static ParsedCollision ParseCollision(byte[] model, Component component)
    {
        int body = component.Offset + 4;
        RequireRange(model, body, 0x1C, "collision header");
        int triangleCount = ReadInt32(model, body);
        int flagCount = ReadInt32(model, body + 4);
        int treeRelative = ReadInt32(model, body + 8);
        int blocksRelative = ReadInt32(model, body + 12);
        int trianglesRelative = ReadInt32(model, body + 16);
        int assignmentsRelative = ReadInt32(model, body + 20);
        int flagsRelative = ReadInt32(model, body + 24);
        if (triangleCount <= 0 || triangleCount > 0x7FFF || flagCount < 0 || flagCount > triangleCount ||
            treeRelative != CollisionTreeRelativeOffset || blocksRelative != CollisionBlocksRelativeOffset ||
            trianglesRelative != CollisionTrianglesRelativeOffset || blocksRelative <= treeRelative ||
            trianglesRelative <= blocksRelative || assignmentsRelative <= trianglesRelative ||
            flagsRelative <= assignmentsRelative)
        {
            throw new InvalidDataException("The collision header is outside the verified native layout.");
        }
        int treeOffset = checked(body + treeRelative);
        int blocksOffset = checked(body + blocksRelative);
        int triangleOffset = checked(body + trianglesRelative);
        int assignmentsOffset = checked(body + assignmentsRelative);
        int flagsOffset = checked(body + flagsRelative);
        int end = component.Offset + component.ByteLength;
        if (treeOffset != component.Offset + 0x20 ||
            triangleOffset + (triangleCount * 12L) != assignmentsOffset ||
            assignmentsOffset + triangleCount > flagsOffset ||
            flagsOffset + flagCount != end ||
            model.AsSpan(assignmentsOffset + triangleCount, flagsOffset - assignmentsOffset - triangleCount)
                .IndexOfAnyExcept((byte)0) >= 0)
        {
            throw new InvalidDataException("The collision tables do not exactly partition their component.");
        }
        return new(
            component,
            triangleCount,
            flagCount,
            treeRelative,
            blocksRelative,
            trianglesRelative,
            assignmentsRelative,
            flagsRelative,
            treeOffset,
            blocksOffset,
            triangleOffset,
            assignmentsOffset,
            flagsOffset,
            blocksOffset - treeOffset,
            triangleOffset - blocksOffset,
            model.AsSpan(treeOffset, blocksOffset - treeOffset).ToArray(),
            model.AsSpan(blocksOffset, triangleOffset - blocksOffset).ToArray(),
            model.AsSpan(triangleOffset, triangleCount * 12).ToArray(),
            model.AsSpan(assignmentsOffset, triangleCount).ToArray(),
            model.AsSpan(flagsOffset, flagCount).ToArray());
    }

    private static void RequireComponent(ParsedModel model, string name, int offset, int length)
    {
        Component component = model.Components.Single(item => item.Name == name);
        if (component.Offset != offset || component.ByteLength != length)
        {
            throw new InvalidDataException(
                $"The {name} component resolved to 0x{component.Offset:X}/0x{component.ByteLength:X}, expected 0x{offset:X}/0x{length:X}.");
        }
    }

    private static ManifestValidation ValidateManifest(
        ParsedModel foundation,
        UnusedLevel65FoundationTerrainManifest manifest,
        UnusedLevel65FoundationTerrainSafetyLimits? limits)
    {
        if (manifest.Vertices == null || manifest.LockedFoundationTile == null || manifest.OptionalTiles == null)
            throw new InvalidDataException("The terrain manifest is incomplete.");
        if (manifest.LockedFoundationTileId != LockedTileId ||
            manifest.LockedFoundationTile.TileId != LockedTileId)
            throw new InvalidDataException("The first foundation tile must remain locked.");

        Dictionary<string, UnusedLevel65FoundationTerrainPoint> vertices = new(StringComparer.Ordinal);
        foreach (UnusedLevel65FoundationTerrainVertex vertex in manifest.Vertices)
        {
            if (string.IsNullOrWhiteSpace(vertex.Handle) || !vertices.TryAdd(vertex.Handle, vertex.Point))
                throw new InvalidDataException("Terrain vertex handles must be nonempty and unique.");
        }
        RequireVertex(vertices, HandleA, PointA, "locked A");
        RequireVertex(vertices, HandleB, PointB, "locked B");
        RequireVertex(vertices, HandleC, PointC, "locked C");
        ValidateTileEnvelope(foundation, manifest.LockedFoundationTile, vertices, isLocked: true);

        HashSet<string> tileIds = new(StringComparer.Ordinal) { LockedTileId };
        foreach (UnusedLevel65FoundationTerrainTile tile in manifest.OptionalTiles)
        {
            if (string.IsNullOrWhiteSpace(tile.TileId) || !tileIds.Add(tile.TileId))
                throw new InvalidDataException("Terrain tile ids must be nonempty and unique.");
            ValidateTileEnvelope(foundation, tile, vertices, isLocked: false);
        }

        string[] lockedHandles = [HandleA, HandleB, HandleC];
        HashSet<string> additionalHandles = manifest.OptionalTiles
            .SelectMany(tile => tile.LowDetailVertexHandles)
            .Where(handle => !lockedHandles.Contains(handle, StringComparer.Ordinal))
            .ToHashSet(StringComparer.Ordinal);
        int additionalVertexCount = additionalHandles.Count;
        int optionalFaceCount = manifest.OptionalTiles.Count;
        ParsedSector sector = foundation.Sectors[TargetSectorIndex];
        if (sector.LowDetailVertexCount + additionalVertexCount >= 64)
            throw new InvalidDataException("The optional terrain manifest exhausts the LP six-bit vertex index capacity.");
        if (sector.HighDetailVertexCount + additionalVertexCount >= 256)
            throw new InvalidDataException("The optional terrain manifest exhausts the HP byte vertex index capacity.");
        if (sector.LowDetailFaceCount + optionalFaceCount >= 256 ||
            sector.HighDetailFaceCount + optionalFaceCount >= 256)
            throw new InvalidDataException("The optional terrain manifest exhausts a sector face-count byte.");

        int maximumTriangleCount = limits?.MaximumTriangleCount ?? 0x7FFF;
        if (FoundationTriangleCount + optionalFaceCount > maximumTriangleCount ||
            FoundationTriangleCount + optionalFaceCount > 0x7FFF)
            throw new InvalidDataException("The optional terrain manifest exhausts the collision 15-bit triangle index range.");

        int sceneGrowth = checked((additionalVertexCount * 8) + (optionalFaceCount * 24));
        int collisionGrowth = checked((optionalFaceCount * 12) +
            (Align4(FoundationTriangleCount + optionalFaceCount) - Align4(FoundationTriangleCount)));
        int tailBudget = limits?.AvailableModelTailBytes ?? FoundationZeroTailBytes;
        if (sceneGrowth + collisionGrowth > tailBudget)
        {
            throw new InvalidDataException(
                $"The terrain transaction needs 0x{sceneGrowth + collisionGrowth:X} model-tail bytes, but only 0x{tailBudget:X} are authorized.");
        }

        ParsedCollision collision = foundation.Collision;
        NativeCollisionIndex index = DecodeCollisionIndex(
            collision.TreeBytes,
            collision.BlockBytes,
            FoundationUsedBlockBytes,
            collision.TriangleCount);
        IReadOnlyList<IReadOnlyList<int>> occlusionGroups = ParseOcclusionGroups(
            foundation.Bytes,
            foundation.Components.Single(component => component.Name == "occlusion"),
            foundation.Sectors.Count);
        List<UnusedLevel65FoundationTerrainPoint[]> optionalPoints = [];
        List<(int TriangleIndex, UnusedLevel65FoundationCollisionCell[] Cells)> additions = [];
        for (int tileIndex = 0; tileIndex < manifest.OptionalTiles.Count; tileIndex++)
        {
            UnusedLevel65FoundationTerrainTile tile = manifest.OptionalTiles[tileIndex];
            UnusedLevel65FoundationTerrainPoint[] points = tile.HighDetailVertexHandles
                .Select(handle => vertices[handle])
                .ToArray();
            optionalPoints.Add(points);
            if (tile.OcclusionAssignment < 0 || tile.OcclusionAssignment >= occlusionGroups.Count ||
                !occlusionGroups[tile.OcclusionAssignment].Contains(tile.SectorIndex))
            {
                throw new InvalidDataException(
                    $"Occlusion assignment {tile.OcclusionAssignment} does not own sector {tile.SectorIndex}.");
            }
            UnusedLevel65FoundationCollisionCell[] cells = CollisionTouchedCells(points).ToArray();
            if (cells.Length == 0 || cells.Any(cell => !index.Cells.ContainsKey(cell)))
                throw new InvalidDataException("An authored collision triangle touches a cell absent from the native tree.");
            additions.Add((FoundationTriangleCount + tileIndex, cells));
        }
        CollisionRepack preview = RepackCollisionIndex(
            index,
            additions,
            FoundationTriangleCount + optionalFaceCount,
            limits?.CollisionBlockCapacityBytes ?? CollisionBlocksCapacityBytes);

        if (manifest.OptionalTiles.Count > 1)
            throw new InvalidDataException("This exact static gate accepts at most one removable optional terrain tile.");
        if (manifest.OptionalTiles.Count == 1)
            ValidateExactOptionalManifest(manifest);
        else if (manifest.Vertices.Count != 3)
            throw new InvalidDataException("The empty optional manifest may contain only the three locked foundation handles.");

        return new(
            vertices,
            optionalPoints.Count == 1 ? optionalPoints[0] : Array.Empty<UnusedLevel65FoundationTerrainPoint>(),
            preview,
            sceneGrowth,
            collisionGrowth);
    }

    private static void ValidateTileEnvelope(
        ParsedModel foundation,
        UnusedLevel65FoundationTerrainTile tile,
        IReadOnlyDictionary<string, UnusedLevel65FoundationTerrainPoint> vertices,
        bool isLocked)
    {
        if (tile.SectorIndex != TargetSectorIndex)
            throw new InvalidDataException("The first terrain composer gate accepts only scene sector 213.");
        if (tile.LowDetailVertexHandles == null || tile.HighDetailVertexHandles == null ||
            tile.LowDetailVertexHandles.Count != 3 || tile.HighDetailVertexHandles.Count != 3)
            throw new InvalidDataException("Every authored terrain tile must have exactly three LP and three HP handles.");
        if (!tile.LowDetailVertexHandles.SequenceEqual(tile.HighDetailVertexHandles, StringComparer.Ordinal))
            throw new InvalidDataException("The authored LP and HP topology handles do not match exactly.");
        if (tile.LowDetailVertexHandles.Distinct(StringComparer.Ordinal).Count() != 3 ||
            tile.LowDetailVertexHandles.Any(handle => !vertices.ContainsKey(handle)))
            throw new InvalidDataException("An authored terrain face has missing or repeated topology handles.");
        if (tile.HighDetailTextureId != 25)
            throw new InvalidDataException("The first removable terrain gate accepts only resident material T25.");
        if (!tile.OrdinaryCollision || tile.CollisionSurfaceIndex != 0 || tile.CollisionZFlags != 0)
            throw new InvalidDataException("Nonordinary collision flags require a separate flag-promotion gate.");
        if (tile.OcclusionAssignment is < 0 or > 0xFF)
            throw new InvalidDataException("The authored collision occlusion assignment is outside one byte.");

        UnusedLevel65FoundationTerrainPoint[] points = tile.HighDetailVertexHandles
            .Select(handle => vertices[handle])
            .ToArray();
        (long NormalX, long NormalY, long NormalZ) = CollisionNormal(points[0], points[1], points[2]);
        if (NormalX == 0 && NormalY == 0 && NormalZ == 0)
            throw new InvalidDataException("The authored terrain triangle has zero area.");
        if (NormalZ <= 0)
            throw new InvalidDataException("The authored terrain triangle does not have upward winding.");
        ParsedSector sector = foundation.Sectors[TargetSectorIndex];
        if (points.Any(point => !PointInsideSectorCullSphere(sector.Bytes, point)))
            throw new InvalidDataException("An authored terrain point is outside the unchanged sector-213 cull sphere.");
        foreach (UnusedLevel65FoundationTerrainPoint point in points)
            _ = EncodeSceneVertex(sector.Bytes, point);
        _ = EncodeCollisionTriangle(points, tile.CollisionZFlags);

        if (isLocked)
        {
            if (!tile.LowDetailVertexHandles.SequenceEqual(new[] { HandleA, HandleB, HandleC }, StringComparer.Ordinal) ||
                !points.SequenceEqual(new[] { PointA, PointB, PointC }) ||
                tile.OcclusionAssignment != 0)
                throw new InvalidDataException("The locked foundation tile topology or ownership changed.");
        }
    }

    private static void ValidateExactOptionalManifest(UnusedLevel65FoundationTerrainManifest manifest)
    {
        if (manifest.OptionalTiles.Count != 1)
            throw new InvalidDataException("The exact optional terrain oracle requires one active tile.");
        Dictionary<string, UnusedLevel65FoundationTerrainPoint> vertices;
        try
        {
            vertices = manifest.Vertices
                .ToDictionary(vertex => vertex.Handle, vertex => vertex.Point, StringComparer.Ordinal);
        }
        catch (ArgumentException ex)
        {
            throw new InvalidDataException("The exact optional terrain oracle contains duplicate vertex handles.", ex);
        }
        UnusedLevel65FoundationTerrainTile tile = manifest.OptionalTiles[0];
        if (manifest.Vertices.Count != 4 ||
            manifest.LockedFoundationTileId != LockedTileId ||
            manifest.LockedFoundationTile.TileId != LockedTileId ||
            !manifest.LockedFoundationTile.LowDetailVertexHandles.SequenceEqual(
                new[] { HandleA, HandleB, HandleC }, StringComparer.Ordinal) ||
            !manifest.LockedFoundationTile.HighDetailVertexHandles.SequenceEqual(
                new[] { HandleA, HandleB, HandleC }, StringComparer.Ordinal) ||
            manifest.LockedFoundationTile.HighDetailTextureId != 25 ||
            manifest.LockedFoundationTile.CollisionSurfaceIndex != 0 ||
            manifest.LockedFoundationTile.OcclusionAssignment != 0 ||
            !manifest.LockedFoundationTile.OrdinaryCollision ||
            manifest.LockedFoundationTile.CollisionZFlags != 0 ||
            !vertices.TryGetValue(HandleA, out UnusedLevel65FoundationTerrainPoint pointA) || pointA != PointA ||
            !vertices.TryGetValue(HandleB, out UnusedLevel65FoundationTerrainPoint pointB) || pointB != PointB ||
            !vertices.TryGetValue(HandleC, out UnusedLevel65FoundationTerrainPoint pointC) || pointC != PointC ||
            !vertices.TryGetValue(HandleD, out UnusedLevel65FoundationTerrainPoint pointD) || pointD != PointD ||
            tile.TileId != OptionalTileId || tile.SectorIndex != TargetSectorIndex ||
            !tile.LowDetailVertexHandles.SequenceEqual(new[] { HandleB, HandleD, HandleC }, StringComparer.Ordinal) ||
            !tile.HighDetailVertexHandles.SequenceEqual(new[] { HandleB, HandleD, HandleC }, StringComparer.Ordinal) ||
            tile.HighDetailTextureId != 25 || tile.CollisionSurfaceIndex != 0 ||
            tile.OcclusionAssignment != 0 || !tile.OrdinaryCollision || tile.CollisionZFlags != 0)
        {
            throw new InvalidDataException("The optional terrain tile differs from the exact B,D,C two-tile oracle.");
        }
    }

    private static void RequireVertex(
        IReadOnlyDictionary<string, UnusedLevel65FoundationTerrainPoint> vertices,
        string handle,
        UnusedLevel65FoundationTerrainPoint expected,
        string label)
    {
        if (!vertices.TryGetValue(handle, out UnusedLevel65FoundationTerrainPoint actual) || actual != expected)
            throw new InvalidDataException($"The {label} vertex handle changed.");
    }

    private static byte[] BuildTwoTileEnvironment(
        ParsedModel foundation,
        UnusedLevel65FoundationTerrainManifest manifest)
    {
        Component environment = foundation.Components.Single(component => component.Name == "environment");
        ParsedSector sector = foundation.Sectors[TargetSectorIndex];
        byte[] source = sector.Bytes;
        int lpVertices = source[16];
        int lpColors = source[17];
        int lpFaces = source[18];
        int hpVertices = source[20];
        int hpColors = source[21];
        int hpFaces = source[22];
        if (lpVertices != 38 || lpColors != 48 || lpFaces != 22 ||
            hpVertices != 143 || hpColors != 185 || hpFaces != 114)
            throw new InvalidDataException("Sector 213 counts changed before two-tile composition.");

        int lpVertexStart = 28;
        int lpColorStart = lpVertexStart + (lpVertices * 4);
        int lpFaceStart = lpColorStart + (lpColors * 4);
        int hpVertexStart = lpFaceStart + (lpFaces * 8);
        int hpColorStart = hpVertexStart + (hpVertices * 4);
        int hpFaceStart = hpColorStart + (hpColors * 8);
        if (hpFaceStart + (hpFaces * 16) != source.Length)
            throw new InvalidDataException("Sector 213 arrays no longer exactly partition the source sector.");

        Dictionary<string, UnusedLevel65FoundationTerrainPoint> vertices = manifest.Vertices
            .ToDictionary(vertex => vertex.Handle, vertex => vertex.Point, StringComparer.Ordinal);
        uint dWord = EncodeSceneVertex(source, vertices[HandleD]);
        if (dWord != 0x41E7D8A0)
            throw new InvalidDataException($"The removable D vertex encoded as 0x{dWord:X8}.");
        byte[] dBytes = new byte[4];
        BinaryPrimitives.WriteUInt32LittleEndian(dBytes, dWord);
        byte[] lowDetailFace = Convert.FromHexString(OptionalLowDetailFaceHex);
        byte[] highDetailFace = Convert.FromHexString(OptionalHighDetailFaceHex);

        byte[] authoredSector = new byte[source.Length + 0x20];
        source.AsSpan(0, 28).CopyTo(authoredSector);
        authoredSector[16] = 39;
        authoredSector[18] = 23;
        authoredSector[20] = 144;
        authoredSector[22] = 115;
        int output = 28;
        source.AsSpan(lpVertexStart, lpVertices * 4).CopyTo(authoredSector.AsSpan(output));
        output += lpVertices * 4;
        dBytes.CopyTo(authoredSector, output);
        output += 4;
        source.AsSpan(lpColorStart, lpColors * 4).CopyTo(authoredSector.AsSpan(output));
        output += lpColors * 4;
        source.AsSpan(lpFaceStart, lpFaces * 8).CopyTo(authoredSector.AsSpan(output));
        output += lpFaces * 8;
        lowDetailFace.CopyTo(authoredSector, output);
        output += 8;
        source.AsSpan(hpVertexStart, hpVertices * 4).CopyTo(authoredSector.AsSpan(output));
        output += hpVertices * 4;
        dBytes.CopyTo(authoredSector, output);
        output += 4;
        source.AsSpan(hpColorStart, hpColors * 8).CopyTo(authoredSector.AsSpan(output));
        output += hpColors * 8;
        source.AsSpan(hpFaceStart, hpFaces * 16).CopyTo(authoredSector.AsSpan(output));
        output += hpFaces * 16;
        highDetailFace.CopyTo(authoredSector, output);
        output += 16;
        if (output != authoredSector.Length || Hash(authoredSector) != ExpectedTwoTileSectorSha256)
            throw new InvalidDataException("The exact sector-213 two-tile bytes changed.");

        int relativeSectorStart = sector.Offset - environment.Offset;
        int relativeSectorEnd = relativeSectorStart + sector.ByteLength;
        byte[] authoredEnvironment = new byte[environment.ByteLength + 0x20];
        environment.Bytes.AsSpan(0, relativeSectorStart).CopyTo(authoredEnvironment);
        authoredSector.CopyTo(authoredEnvironment, relativeSectorStart);
        environment.Bytes.AsSpan(relativeSectorEnd).CopyTo(
            authoredEnvironment.AsSpan(relativeSectorStart + authoredSector.Length));
        WriteInt32(authoredEnvironment, 0, authoredEnvironment.Length);
        for (int index = TargetSectorIndex + 1; index < foundation.Sectors.Count; index++)
        {
            int pointerOffset = 8 + (index * 4);
            WriteInt32(
                authoredEnvironment,
                pointerOffset,
                checked(ReadInt32(environment.Bytes, pointerOffset) + 0x20));
        }
        if (Hash(authoredEnvironment) != ExpectedTwoTileEnvironmentSha256)
            throw new InvalidDataException("The exact two-tile environment bytes changed.");
        return authoredEnvironment;
    }

    private static CollisionComposition BuildTwoTileCollision(
        ParsedModel foundation,
        ManifestValidation validation,
        UnusedLevel65FoundationTerrainSafetyLimits? limits)
    {
        ParsedCollision source = foundation.Collision;
        if (validation.OptionalPoints.Length != 3)
            throw new InvalidDataException("The optional collision plan did not resolve exactly three points.");
        byte[] appendedTriangle = EncodeCollisionTriangle(validation.OptionalPoints, zFlags: 0);
        if (Convert.ToHexString(appendedTriangle) != OptionalCollisionHex)
            throw new InvalidDataException("The exact B,D,C collision row encoding changed.");

        NativeCollisionIndex native = DecodeCollisionIndex(
            source.TreeBytes,
            source.BlockBytes,
            FoundationUsedBlockBytes,
            source.TriangleCount);
        CollisionRepack repack = RepackCollisionIndex(
            native,
            [(FoundationTriangleCount, OptionalTouchedCells)],
            FoundationTriangleCount + 1,
            limits?.CollisionBlockCapacityBytes ?? CollisionBlocksCapacityBytes);
        if (repack.UsedBlockBytes != TwoTileUsedBlockBytes ||
            Hash(repack.TreeBytes) != ExpectedTwoTileTreeSha256 ||
            Hash(repack.BlockBytes) != ExpectedTwoTileBlocksSha256 ||
            !repack.ChangedCells.SequenceEqual(OptionalTouchedCells))
        {
            throw new InvalidDataException("The exact second-tile collision index repack changed.");
        }

        int newTriangleCount = FoundationTriangleCount + 1;
        int assignmentsRelative = CollisionTrianglesRelativeOffset + (newTriangleCount * 12);
        int flagsRelative = assignmentsRelative + Align4(newTriangleCount);
        int newLength = checked(4 + flagsRelative + CollisionFlagCount);
        if (assignmentsRelative != 0x5848C || flagsRelative != 0x5D1F0 || newLength != 0x5FAF8)
            throw new InvalidDataException("The collision triangle/assignment alignment calculation changed.");
        byte[] result = new byte[newLength];
        WriteInt32(result, 0, newLength);
        WriteInt32(result, 4, newTriangleCount);
        WriteInt32(result, 8, CollisionFlagCount);
        WriteInt32(result, 12, CollisionTreeRelativeOffset);
        WriteInt32(result, 16, CollisionBlocksRelativeOffset);
        WriteInt32(result, 20, CollisionTrianglesRelativeOffset);
        WriteInt32(result, 24, assignmentsRelative);
        WriteInt32(result, 28, flagsRelative);
        int treeOffset = 4 + CollisionTreeRelativeOffset;
        int blocksOffset = 4 + CollisionBlocksRelativeOffset;
        int trianglesOffset = 4 + CollisionTrianglesRelativeOffset;
        int assignmentsOffset = 4 + assignmentsRelative;
        int flagsOffset = 4 + flagsRelative;
        repack.TreeBytes.CopyTo(result, treeOffset);
        repack.BlockBytes.CopyTo(result, blocksOffset);
        source.TriangleBytes.CopyTo(result, trianglesOffset);
        appendedTriangle.CopyTo(result, trianglesOffset + source.TriangleBytes.Length);
        source.AssignmentBytes.CopyTo(result, assignmentsOffset);
        result[assignmentsOffset + FoundationTriangleCount] = 0;
        if (result.AsSpan(assignmentsOffset + newTriangleCount, Align4(newTriangleCount) - newTriangleCount)
            .IndexOfAnyExcept((byte)0) >= 0)
            throw new InvalidDataException("The appended collision assignment alignment is not zero.");
        source.FlagBytes.CopyTo(result, flagsOffset);
        if (Hash(result) != ExpectedTwoTileCollisionSha256)
            throw new InvalidDataException("The exact two-tile collision component hash changed.");
        return new(
            result,
            repack.TreeBytes,
            repack.BlockBytes,
            repack.UsedBlockBytes,
            native.Cells.Count,
            repack.GroupStartCount,
            repack.ChangedCells,
            Hash(repack.TreeBytes),
            Hash(repack.BlockBytes));
    }

    private static byte[] ComposeModel(
        ParsedModel foundation,
        byte[] environment,
        byte[] collision)
    {
        List<byte[]> parts = [];
        foreach (Component component in foundation.Components)
        {
            parts.Add(component.Name switch
            {
                "environment" => environment,
                "collision" => collision,
                _ => component.Bytes
            });
        }
        int used = parts.Sum(part => part.Length);
        if (used != 0x94568 || used > ModelByteLength)
            throw new InvalidDataException($"The two-tile model uses unexpected 0x{used:X} bytes.");
        byte[] result = new byte[ModelByteLength];
        int output = 0;
        foreach (byte[] part in parts)
        {
            part.CopyTo(result, output);
            output += part.Length;
        }
        if (result.AsSpan(output).IndexOfAnyExcept((byte)0) >= 0)
            throw new InvalidDataException("The composed model tail is not exact zero.");
        return result;
    }

    private static NativeCollisionIndex DecodeCollisionIndex(
        byte[] treeBytes,
        byte[] blockBytes,
        int usedBlockBytes,
        int triangleCount)
    {
        if ((treeBytes.Length & 1) != 0 || (blockBytes.Length & 1) != 0 ||
            usedBlockBytes <= 0 || (usedBlockBytes & 1) != 0 || usedBlockBytes > blockBytes.Length)
            throw new InvalidDataException("The native collision index capacities are invalid.");
        int usedWords = usedBlockBytes / 2;
        List<int> starts = [];
        for (int wordOffset = 0; wordOffset < usedWords; wordOffset++)
        {
            ushort word = BinaryPrimitives.ReadUInt16LittleEndian(blockBytes.AsSpan(wordOffset * 2, 2));
            if ((word & 0x8000) != 0)
                starts.Add(wordOffset);
        }
        if (starts.Count < 2 || starts[^1] != usedWords - 1 ||
            BinaryPrimitives.ReadUInt16LittleEndian(blockBytes.AsSpan(starts[^1] * 2, 2)) != 0x8000 ||
            blockBytes.AsSpan(usedBlockBytes).IndexOfAnyExcept((byte)0) >= 0)
            throw new InvalidDataException("The collision blocks lack their exact terminal sentinel/zero tail.");
        Dictionary<int, int[]> groups = [];
        for (int group = 0; group < starts.Count - 1; group++)
        {
            int start = starts[group];
            int end = starts[group + 1];
            int[] sequence = new int[end - start];
            for (int index = 0; index < sequence.Length; index++)
            {
                ushort word = BinaryPrimitives.ReadUInt16LittleEndian(blockBytes.AsSpan((start + index) * 2, 2));
                if ((index == 0) != ((word & 0x8000) != 0))
                    throw new InvalidDataException("A collision block group has an invalid start marker.");
                int triangleIndex = word & 0x7FFF;
                if (triangleIndex >= triangleCount)
                    throw new InvalidDataException("A collision block references an out-of-range triangle.");
                sequence[index] = triangleIndex;
            }
            groups.Add(start, sequence);
        }

        int ReadTreeWord(int byteOffset)
        {
            if (byteOffset < 0 || (byteOffset & 1) != 0 || byteOffset + 2 > treeBytes.Length)
                throw new InvalidDataException("A collision tree word is outside its fixed capacity.");
            return BinaryPrimitives.ReadUInt16LittleEndian(treeBytes.AsSpan(byteOffset, 2));
        }
        int ReadSegmentLength(int byteOffset)
        {
            int length = ReadTreeWord(byteOffset);
            if (length < 0 || length > 256 || byteOffset + ((length + 1L) * 2) > treeBytes.Length)
                throw new InvalidDataException("A collision tree segment length is invalid.");
            return length;
        }

        Dictionary<UnusedLevel65FoundationCollisionCell, CollisionCellBinding> cells = [];
        int zLength = ReadSegmentLength(0);
        for (int z = 0; z < zLength; z++)
        {
            int ySegment = ReadTreeWord((z + 1) * 2);
            if (ySegment == 0xFFFF)
                continue;
            int yLength = ReadSegmentLength(ySegment);
            for (int y = 0; y < yLength; y++)
            {
                int xSegment = ReadTreeWord(ySegment + ((y + 1) * 2));
                if (xSegment == 0xFFFF)
                    continue;
                int xLength = ReadSegmentLength(xSegment);
                for (int x = 0; x < xLength; x++)
                {
                    int pointerOffset = xSegment + ((x + 1) * 2);
                    int blockWordOffset = ReadTreeWord(pointerOffset);
                    if (blockWordOffset == 0xFFFF)
                        continue;
                    if (!groups.TryGetValue(blockWordOffset, out int[]? sequence))
                        throw new InvalidDataException("A collision cell points outside a real block group.");
                    UnusedLevel65FoundationCollisionCell cell = new(x, y, z);
                    if (!cells.TryAdd(cell, new(cell, pointerOffset, blockWordOffset, sequence.ToArray())))
                        throw new InvalidDataException("A collision cell is represented more than once.");
                }
            }
        }
        int[] referenced = cells.Values.Select(binding => binding.SourceBlockWordOffset).Distinct().Order().ToArray();
        if (!referenced.SequenceEqual(groups.Keys.Order()))
            throw new InvalidDataException("The collision blocks contain an unreferenced group.");
        return new(treeBytes.ToArray(), blockBytes.ToArray(), cells, starts.Count, usedBlockBytes);
    }

    private static CollisionRepack RepackCollisionIndex(
        NativeCollisionIndex native,
        IReadOnlyList<(int TriangleIndex, UnusedLevel65FoundationCollisionCell[] Cells)> additions,
        int triangleCount,
        int blockCapacityLimit)
    {
        if (blockCapacityLimit < 0 || blockCapacityLimit > native.BlockBytes.Length)
            throw new InvalidDataException("The authorized collision block capacity is invalid.");
        Dictionary<UnusedLevel65FoundationCollisionCell, List<int>> desired = native.Cells
            .ToDictionary(pair => pair.Key, pair => pair.Value.OrderedTriangleIndexes.ToList());
        HashSet<UnusedLevel65FoundationCollisionCell> changed = [];
        foreach ((int triangleIndex, UnusedLevel65FoundationCollisionCell[] cells) in additions)
        {
            if (triangleIndex < 0 || triangleIndex >= triangleCount || triangleIndex > 0x7FFF)
                throw new InvalidDataException("An appended collision row exceeds the 15-bit block-list index.");
            foreach (UnusedLevel65FoundationCollisionCell cell in cells)
            {
                if (!desired.TryGetValue(cell, out List<int>? sequence))
                    throw new InvalidDataException($"Authored collision cell {cell} is absent from the native tree.");
                if (sequence.Contains(triangleIndex))
                    throw new InvalidDataException("An appended collision row is already present in a target cell.");
                sequence.Add(triangleIndex);
                sequence.Sort((left, right) => right.CompareTo(left));
                changed.Add(cell);
            }
        }

        Dictionary<string, int> emitted = new(StringComparer.Ordinal);
        Dictionary<UnusedLevel65FoundationCollisionCell, int> blockOffsets = [];
        List<ushort> words = [];
        foreach (CollisionCellBinding binding in native.Cells.Values
                     .OrderBy(binding => binding.SourceBlockWordOffset)
                     .ThenBy(binding => binding.Cell.Z)
                     .ThenBy(binding => binding.Cell.Y)
                     .ThenBy(binding => binding.Cell.X))
        {
            int[] sequence = desired[binding.Cell].ToArray();
            if (sequence.Length == 0 || !IsStrictlyDescending(sequence))
                throw new InvalidDataException($"Collision cell {binding.Cell} lost native descending order.");
            string key = string.Join(',', sequence);
            if (!emitted.TryGetValue(key, out int wordOffset))
            {
                wordOffset = words.Count;
                if (wordOffset > ushort.MaxValue)
                    throw new InvalidDataException("A collision block pointer exceeds 16 bits.");
                for (int index = 0; index < sequence.Length; index++)
                {
                    ushort word = checked((ushort)sequence[index]);
                    if (index == 0)
                        word |= 0x8000;
                    words.Add(word);
                }
                emitted.Add(key, wordOffset);
            }
            blockOffsets.Add(binding.Cell, wordOffset);
        }
        words.Add(0x8000);
        int usedBytes = checked(words.Count * 2);
        if (usedBytes > blockCapacityLimit)
        {
            throw new InvalidDataException(
                $"The native-ordered collision repack needs 0x{usedBytes:X} block bytes; authorized capacity is 0x{blockCapacityLimit:X}.");
        }

        byte[] tree = native.TreeBytes.ToArray();
        foreach (CollisionCellBinding binding in native.Cells.Values)
        {
            BinaryPrimitives.WriteUInt16LittleEndian(
                tree.AsSpan(binding.TreePointerByteOffset, 2),
                checked((ushort)blockOffsets[binding.Cell]));
        }
        byte[] blocks = new byte[native.BlockBytes.Length];
        for (int index = 0; index < words.Count; index++)
            BinaryPrimitives.WriteUInt16LittleEndian(blocks.AsSpan(index * 2, 2), words[index]);

        NativeCollisionIndex readback = DecodeCollisionIndex(tree, blocks, usedBytes, triangleCount);
        if (readback.Cells.Count != native.Cells.Count)
            throw new InvalidDataException("The collision repack changed the native cell count.");
        foreach ((UnusedLevel65FoundationCollisionCell cell, CollisionCellBinding binding) in native.Cells)
        {
            int[] expected = desired[cell].ToArray();
            if (!readback.Cells[cell].OrderedTriangleIndexes.SequenceEqual(expected))
                throw new InvalidDataException($"Collision cell {cell} failed semantic readback.");
            if (!changed.Contains(cell) && !binding.OrderedTriangleIndexes.SequenceEqual(expected))
                throw new InvalidDataException($"Unchanged collision cell {cell} changed sequence.");
        }
        UnusedLevel65FoundationCollisionCell[] orderedChanged = changed
            .OrderBy(cell => cell.Z).ThenBy(cell => cell.Y).ThenBy(cell => cell.X).ToArray();
        return new(tree, blocks, usedBytes, readback.GroupStartCount, orderedChanged, readback);
    }

    private static bool IsStrictlyDescending(IReadOnlyList<int> values)
    {
        for (int index = 1; index < values.Count; index++)
        {
            if (values[index - 1] <= values[index])
                return false;
        }
        return true;
    }

    private static uint EncodeSceneVertex(byte[] sector, UnusedLevel65FoundationTerrainPoint point)
    {
        uint radiusAndFlags = BinaryPrimitives.ReadUInt16LittleEndian(sector.AsSpan(4, 2));
        uint xy = BinaryPrimitives.ReadUInt32LittleEndian(sector.AsSpan(8, 4));
        uint z = BinaryPrimitives.ReadUInt32LittleEndian(sector.AsSpan(12, 4));
        int sectorX = (int)((xy >> 16) & 0xFFFF);
        int sectorY = (int)(xy & 0xFFFF);
        int sectorZ = (int)(((z >> 14) & 0xFFFF) >> 2);
        bool flat = ((radiusAndFlags >> 12) & 1) == 1;
        int encodedX = point.X - sectorX;
        int encodedY = point.Y - sectorY;
        int encodedZ = flat ? (point.Z * 8) - sectorZ : point.Z - sectorZ;
        if (encodedX is < 0 or > 2047 || encodedY is < 0 or > 2047 || encodedZ is < 0 or > 1023)
            throw new InvalidDataException($"Terrain point {point} cannot pack into sector 213.");
        return ((uint)encodedX << 21) | ((uint)encodedY << 10) | (uint)encodedZ;
    }

    private static UnusedLevel65FoundationTerrainPoint DecodeSceneVertex(byte[] sector, uint word)
    {
        uint xy = BinaryPrimitives.ReadUInt32LittleEndian(sector.AsSpan(8, 4));
        uint z = BinaryPrimitives.ReadUInt32LittleEndian(sector.AsSpan(12, 4));
        int sectorX = (int)(xy >> 16);
        int sectorY = (int)(xy & 0xFFFF);
        int sectorZ = (int)((z >> 14) & 0xFFFF) >> 2;
        int x = sectorX + (int)(((word >> 19) & 0x1FFC) >> 2);
        int y = sectorY + (int)(((word >> 8) & 0x1FFC) >> 2);
        int decodedZ = sectorZ + (int)(((word << 3) & 0x1FFC) >> 3);
        if (((BinaryPrimitives.ReadUInt16LittleEndian(sector.AsSpan(4, 2)) >> 12) & 1) == 1)
            decodedZ >>= 3;
        return new(x, y, decodedZ);
    }

    private static bool PointInsideSectorCullSphere(
        byte[] sector,
        UnusedLevel65FoundationTerrainPoint point)
    {
        uint xy = BinaryPrimitives.ReadUInt32LittleEndian(sector.AsSpan(0, 4));
        int centerX = (int)(xy >> 16);
        int centerY = (int)(xy & 0xFFFF);
        int centerZ = BinaryPrimitives.ReadUInt16LittleEndian(sector.AsSpan(6, 2));
        int radius = BinaryPrimitives.ReadUInt16LittleEndian(sector.AsSpan(4, 2)) & 0x0FFF;
        long dx = point.X - centerX;
        long dy = point.Y - centerY;
        long dz = point.Z - centerZ;
        return (dx * dx) + (dy * dy) + (dz * dz) <= (long)radius * radius;
    }

    private static byte[] EncodeCollisionTriangle(
        IReadOnlyList<UnusedLevel65FoundationTerrainPoint> points,
        uint zFlags)
    {
        if (points.Count != 3)
            throw new InvalidDataException("A collision triangle must contain exactly three points.");
        UnusedLevel65FoundationTerrainPoint p1 = points[0];
        UnusedLevel65FoundationTerrainPoint p2 = points[1];
        UnusedLevel65FoundationTerrainPoint p3 = points[2];
        if (!TrySigned9(p2.X - p1.X, out uint p2Dx) || !TrySigned9(p3.X - p1.X, out uint p3Dx) ||
            !TrySigned9(p2.Y - p1.Y, out uint p2Dy) || !TrySigned9(p3.Y - p1.Y, out uint p3Dy) ||
            p1.X is < 0 or > 0x3FFF || p1.Y is < 0 or > 0x3FFF || p1.Z is < 0 or > 0x3FFF ||
            p2.Z - p1.Z is < 0 or > 0xFF || p3.Z - p1.Z is < 0 or > 0xFF)
            throw new InvalidDataException("The authored collision triangle cannot pack into the native row format.");
        uint xWord = (uint)(p1.X & 0x3FFF) | (p2Dx << 14) | (p3Dx << 23);
        uint yWord = (uint)(p1.Y & 0x3FFF) | (p2Dy << 14) | (p3Dy << 23);
        uint zWord = (zFlags & 0xC000u) | (uint)(p1.Z & 0x3FFF) |
                     ((uint)(p2.Z - p1.Z) << 16) | ((uint)(p3.Z - p1.Z) << 24);
        byte[] result = new byte[12];
        BinaryPrimitives.WriteUInt32LittleEndian(result.AsSpan(0, 4), xWord);
        BinaryPrimitives.WriteUInt32LittleEndian(result.AsSpan(4, 4), yWord);
        BinaryPrimitives.WriteUInt32LittleEndian(result.AsSpan(8, 4), zWord);
        return result;
    }

    private static DecodedCollisionTriangle DecodeCollisionTriangle(ReadOnlySpan<byte> bytes, int index)
    {
        uint x = BinaryPrimitives.ReadUInt32LittleEndian(bytes.Slice(0, 4));
        uint y = BinaryPrimitives.ReadUInt32LittleEndian(bytes.Slice(4, 4));
        uint z = BinaryPrimitives.ReadUInt32LittleEndian(bytes.Slice(8, 4));
        UnusedLevel65FoundationTerrainPoint p1 = new((int)(x & 0x3FFF), (int)(y & 0x3FFF), (int)(z & 0x3FFF));
        UnusedLevel65FoundationTerrainPoint p2 = new(
            p1.X + SignedBits((int)((x >> 14) & 0x1FF), 9),
            p1.Y + SignedBits((int)((y >> 14) & 0x1FF), 9),
            p1.Z + (int)((z >> 16) & 0xFF));
        UnusedLevel65FoundationTerrainPoint p3 = new(
            p1.X + SignedBits((int)((x >> 23) & 0x1FF), 9),
            p1.Y + SignedBits((int)((y >> 23) & 0x1FF), 9),
            p1.Z + (int)((z >> 24) & 0xFF));
        return new(index, [p1, p2, p3]);
    }

    private static bool TrySigned9(int value, out uint encoded)
    {
        if (value is < -256 or > 255)
        {
            encoded = 0;
            return false;
        }
        encoded = (uint)(value & 0x1FF);
        return true;
    }

    private static int SignedBits(int value, int bits)
    {
        int sign = 1 << (bits - 1);
        return (value & sign) != 0 ? value - (1 << bits) : value;
    }

    private static (long X, long Y, long Z) CollisionNormal(
        UnusedLevel65FoundationTerrainPoint a,
        UnusedLevel65FoundationTerrainPoint b,
        UnusedLevel65FoundationTerrainPoint c)
    {
        long abX = b.X - a.X;
        long abY = b.Y - a.Y;
        long abZ = b.Z - a.Z;
        long acX = c.X - a.X;
        long acY = c.Y - a.Y;
        long acZ = c.Z - a.Z;
        return (
            (abY * acZ) - (abZ * acY),
            (abZ * acX) - (abX * acZ),
            (abX * acY) - (abY * acX));
    }

    private static IReadOnlyList<UnusedLevel65FoundationCollisionCell> CollisionTouchedCells(
        IReadOnlyList<UnusedLevel65FoundationTerrainPoint> points)
    {
        int minX = points.Min(point => point.X) >> 8;
        int maxX = points.Max(point => point.X) >> 8;
        int minY = points.Min(point => point.Y) >> 8;
        int maxY = points.Max(point => point.Y) >> 8;
        int minZ = points.Min(point => point.Z) >> 8;
        int maxZ = points.Max(point => point.Z) >> 8;
        List<UnusedLevel65FoundationCollisionCell> cells = [];
        for (int z = minZ; z <= maxZ; z++)
        for (int y = minY; y <= maxY; y++)
        for (int x = minX; x <= maxX; x++)
        {
            if (CollisionTriangleTouchesBlock(points, x, y, z))
                cells.Add(new(x, y, z));
        }
        return cells;
    }

    private static bool CollisionTriangleTouchesBlock(
        IReadOnlyList<UnusedLevel65FoundationTerrainPoint> points,
        int x,
        int y,
        int z)
    {
        int x1 = x << 8;
        int x2 = (x + 1) << 8;
        int y1 = y << 8;
        int y2 = (y + 1) << 8;
        int z1 = z << 8;
        int z2 = (z + 1) << 8;
        if (points.Any(point => point.X >= x1 && point.X < x2 && point.Y >= y1 && point.Y < y2 &&
                                point.Z >= z1 && point.Z < z2))
            return true;
        for (int pointIndex = 0; pointIndex < 3; pointIndex++)
        {
            UnusedLevel65FoundationTerrainPoint current = points[pointIndex];
            UnusedLevel65FoundationTerrainPoint next = points[(pointIndex + 1) % 3];
            if (next.X != current.X)
            {
                foreach (int plane in new[] { x1, x2 })
                {
                    if ((current.X >= plane && next.X <= plane) || (current.X <= plane && next.X >= plane))
                    {
                        int testY = current.Y + (((next.Y - current.Y) * (plane - current.X)) / (next.X - current.X));
                        int testZ = current.Z + (((next.Z - current.Z) * (plane - current.X)) / (next.X - current.X));
                        if (testY >= y1 && testY < y2 && testZ >= z1 && testZ < z2)
                            return true;
                    }
                }
            }
            if (next.Y != current.Y)
            {
                foreach (int plane in new[] { y1, y2 })
                {
                    if ((current.Y >= plane && next.Y <= plane) || (current.Y <= plane && next.Y >= plane))
                    {
                        int testX = current.X + (((next.X - current.X) * (plane - current.Y)) / (next.Y - current.Y));
                        int testZ = current.Z + (((next.Z - current.Z) * (plane - current.Y)) / (next.Y - current.Y));
                        if (testX >= x1 && testX < x2 && testZ >= z1 && testZ < z2)
                            return true;
                    }
                }
            }
        }
        return false;
    }

    private static UnusedLevel65FoundationTerrainExposureReadback BuildAndVerifyExposure(
        ParsedModel foundation,
        IReadOnlyList<UnusedLevel65FoundationTerrainPoint> authored)
    {
        if (!authored.SequenceEqual(new[] { PointB, PointD, PointC }))
            throw new InvalidDataException("The exposure proof received an unexpected optional triangle.");
        List<string> lowDetail = [];
        List<string> highDetail = [];
        foreach (ParsedSector sector in foundation.Sectors)
        {
            byte[] bytes = sector.Bytes;
            int lpVertexStart = 28;
            int lpColorStart = lpVertexStart + (sector.LowDetailVertexCount * 4);
            int lpFaceStart = lpColorStart + (sector.LowDetailColorCount * 4);
            int hpVertexStart = lpFaceStart + (sector.LowDetailFaceCount * 8);
            int hpColorStart = hpVertexStart + (sector.HighDetailVertexCount * 4);
            int hpFaceStart = hpColorStart + (sector.HighDetailColorCount * 8);
            UnusedLevel65FoundationTerrainPoint[] lpVertices = Enumerable.Range(0, sector.LowDetailVertexCount)
                .Select(index => DecodeSceneVertex(
                    bytes,
                    BinaryPrimitives.ReadUInt32LittleEndian(bytes.AsSpan(lpVertexStart + (index * 4), 4))))
                .ToArray();
            UnusedLevel65FoundationTerrainPoint[] hpVertices = Enumerable.Range(0, sector.HighDetailVertexCount)
                .Select(index => DecodeSceneVertex(
                    bytes,
                    BinaryPrimitives.ReadUInt32LittleEndian(bytes.AsSpan(hpVertexStart + (index * 4), 4))))
                .ToArray();
            for (int face = 0; face < sector.LowDetailFaceCount; face++)
            {
                uint word = BinaryPrimitives.ReadUInt32LittleEndian(bytes.AsSpan(lpFaceStart + (face * 8), 4));
                int[] slots = [(int)((word >> 26) & 63), (int)((word >> 20) & 63),
                    (int)((word >> 14) & 63), (int)((word >> 8) & 63)];
                UnusedLevel65FoundationTerrainPoint[] points = ResolveUniqueFace(lpVertices, slots);
                if (points.Length >= 3 && HasPositiveAreaXyOverlap(authored, points))
                    lowDetail.Add($"{sector.Index}:{face}:{points.Min(point => point.Z)}:{points.Max(point => point.Z)}");
            }
            for (int face = 0; face < sector.HighDetailFaceCount; face++)
            {
                int offset = hpFaceStart + (face * 16);
                int[] slots = [bytes[offset], bytes[offset + 1], bytes[offset + 2], bytes[offset + 3]];
                UnusedLevel65FoundationTerrainPoint[] points = ResolveUniqueFace(hpVertices, slots);
                if (points.Length >= 3 && HasPositiveAreaXyOverlap(authored, points))
                    highDetail.Add($"{sector.Index}:{face}:{points.Min(point => point.Z)}:{points.Max(point => point.Z)}");
            }
        }

        List<string> collision = [];
        for (int index = 0; index < foundation.Collision.TriangleCount; index++)
        {
            DecodedCollisionTriangle triangle = DecodeCollisionTriangle(
                foundation.Collision.TriangleBytes.AsSpan(index * 12, 12),
                index);
            (long x, long y, long z) = CollisionNormal(triangle.Points[0], triangle.Points[1], triangle.Points[2]);
            if ((x != 0 || y != 0 || z != 0) && HasPositiveAreaXyOverlap(authored, triangle.Points))
            {
                collision.Add($"{index}:{triangle.Points.Min(point => point.Z)}:{triangle.Points.Max(point => point.Z)}");
            }
        }

        string[] expectedLp = ["124:0:480:480", "213:1:512:512", "213:3:512:512"];
        string[] expectedHp = ["213:37:512:512", "213:43:512:512"];
        string[] expectedCollision = ["1354:512:512", "1400:512:512", "1401:512:512"];
        bool cull = authored.All(point => PointInsideSectorCullSphere(
            foundation.Sectors[TargetSectorIndex].Bytes,
            point));
        if (!lowDetail.SequenceEqual(expectedLp) || !highDetail.SequenceEqual(expectedHp) ||
            !collision.SequenceEqual(expectedCollision) || !cull)
        {
            throw new InvalidDataException(
                $"The two-tile exposure oracle changed: LP=[{string.Join(',', lowDetail)}], " +
                $"HP=[{string.Join(',', highDetail)}], collision=[{string.Join(',', collision)}], cull={cull}.");
        }
        return new(lowDetail, highDetail, collision, true, true, true, true, true);
    }

    private static UnusedLevel65FoundationTerrainPoint[] ResolveUniqueFace(
        IReadOnlyList<UnusedLevel65FoundationTerrainPoint> vertices,
        IReadOnlyList<int> slots)
    {
        if (slots.Any(slot => slot < 0 || slot >= vertices.Count))
            throw new InvalidDataException("A terrain face references a vertex outside its table.");
        List<UnusedLevel65FoundationTerrainPoint> points = [];
        HashSet<int> seen = [];
        foreach (int slot in slots)
        {
            if (seen.Add(slot))
                points.Add(vertices[slot]);
        }
        return points.ToArray();
    }

    private static bool HasPositiveAreaXyOverlap(
        IReadOnlyList<UnusedLevel65FoundationTerrainPoint> target,
        IReadOnlyList<UnusedLevel65FoundationTerrainPoint> candidate)
    {
        if (target.Count != 3 || candidate.Count < 3)
            return false;
        XyPoint[] targetTriangle = target.Select(point => new XyPoint(point.X, point.Y)).ToArray();
        for (int index = 1; index + 1 < candidate.Count; index++)
        {
            XyPoint[] candidateTriangle =
            [
                new(candidate[0].X, candidate[0].Y),
                new(candidate[index].X, candidate[index].Y),
                new(candidate[index + 1].X, candidate[index + 1].Y)
            ];
            if (TriangleIntersectionArea(targetTriangle, candidateTriangle) > 0.000001)
                return true;
        }
        return false;
    }

    private static double TriangleIntersectionArea(
        IReadOnlyList<XyPoint> subject,
        IReadOnlyList<XyPoint> clip)
    {
        List<XyPoint> polygon = subject.ToList();
        double orientation = Cross(clip[0], clip[1], clip[2]);
        if (Math.Abs(orientation) < 0.000001)
            return 0;
        for (int edge = 0; edge < 3 && polygon.Count > 0; edge++)
        {
            XyPoint a = clip[edge];
            XyPoint b = clip[(edge + 1) % 3];
            List<XyPoint> output = [];
            XyPoint previous = polygon[^1];
            bool previousInside = IsInside(a, b, previous, orientation);
            foreach (XyPoint current in polygon)
            {
                bool currentInside = IsInside(a, b, current, orientation);
                if (currentInside != previousInside && TryLineIntersection(previous, current, a, b, out XyPoint intersection))
                    output.Add(intersection);
                if (currentInside)
                    output.Add(current);
                previous = current;
                previousInside = currentInside;
            }
            polygon = output;
        }
        if (polygon.Count < 3)
            return 0;
        double area = 0;
        for (int index = 0; index < polygon.Count; index++)
        {
            XyPoint current = polygon[index];
            XyPoint next = polygon[(index + 1) % polygon.Count];
            area += (current.X * next.Y) - (next.X * current.Y);
        }
        return Math.Abs(area) * 0.5;
    }

    private static bool IsInside(XyPoint a, XyPoint b, XyPoint point, double orientation)
    {
        double value = Cross(a, b, point);
        return orientation > 0 ? value >= -0.000001 : value <= 0.000001;
    }

    private static bool TryLineIntersection(
        XyPoint p1,
        XyPoint p2,
        XyPoint a,
        XyPoint b,
        out XyPoint intersection)
    {
        double dx = p2.X - p1.X;
        double dy = p2.Y - p1.Y;
        double ex = b.X - a.X;
        double ey = b.Y - a.Y;
        double denominator = (dx * ey) - (dy * ex);
        if (Math.Abs(denominator) < 0.000001)
        {
            intersection = default;
            return false;
        }
        double t = (((a.X - p1.X) * ey) - ((a.Y - p1.Y) * ex)) / denominator;
        intersection = new(p1.X + (t * dx), p1.Y + (t * dy));
        return true;
    }

    private static double Cross(XyPoint a, XyPoint b, XyPoint c) =>
        ((b.X - a.X) * (c.Y - a.Y)) - ((b.Y - a.Y) * (c.X - a.X));

    private static IReadOnlyList<IReadOnlyList<int>> ParseOcclusionGroups(
        byte[] model,
        Component component,
        int sectorCount)
    {
        if (ReadInt32(model, component.Offset) != component.ByteLength)
            throw new InvalidDataException("The occlusion component length changed.");
        int environmentPortionLength = ReadInt32(model, component.Offset + 4);
        int environmentPortionEnd = checked(component.Offset + 4 + environmentPortionLength);
        int groupCount = ReadInt32(model, component.Offset + 8);
        int pointerStart = component.Offset + 12;
        if (environmentPortionLength < 8 || environmentPortionEnd > component.Offset + component.ByteLength ||
            groupCount is < 0 or > 256 || pointerStart + (groupCount * 4L) > environmentPortionEnd)
            throw new InvalidDataException("The occlusion group directory is invalid.");
        List<IReadOnlyList<int>> groups = new(groupCount);
        for (int group = 0; group < groupCount; group++)
        {
            int relative = ReadInt32(model, pointerStart + (group * 4));
            int offset = checked(component.Offset + 4 + relative);
            if (offset < pointerStart + (groupCount * 4) || offset >= environmentPortionEnd)
                throw new InvalidDataException("An occlusion group pointer is outside its component.");
            List<int> sectors = [];
            bool terminated = false;
            for (; offset < environmentPortionEnd; offset++)
            {
                int sector = model[offset];
                if (sector == 0xFF)
                {
                    terminated = true;
                    break;
                }
                if (sector >= sectorCount)
                    throw new InvalidDataException("An occlusion group references an out-of-range scene sector.");
                sectors.Add(sector);
            }
            if (!terminated)
                throw new InvalidDataException("An occlusion group lacks its terminator.");
            groups.Add(sectors);
        }
        return groups;
    }

    private static string HashPlan(
        UnusedLevel65FoundationTerrainManifest manifest,
        string outputModelSha256,
        CollisionComposition collision)
    {
        string canonical = string.Join('|', new[]
        {
            ProfileId,
            ExpectedFoundationImageSha256,
            ExpectedFoundationModelSha256,
            outputModelSha256,
            manifest.LockedFoundationTileId,
            string.Join(',', manifest.Vertices.OrderBy(vertex => vertex.Handle, StringComparer.Ordinal)
                .Select(vertex => $"{vertex.Handle}:{vertex.Point.X}:{vertex.Point.Y}:{vertex.Point.Z}")),
            string.Join(',', manifest.OptionalTiles.Select(tile =>
                $"{tile.TileId}:{string.Join('/', tile.LowDetailVertexHandles)}:{tile.HighDetailTextureId}:{tile.CollisionSurfaceIndex}:{tile.OcclusionAssignment}")),
            collision.TreeSha256,
            collision.BlocksSha256,
            collision.UsedBlockBytes.ToString(),
            "writes-bin=false",
            "writes-cue=false",
            "create-bin=false",
            "release=false"
        });
        return Hash(System.Text.Encoding.UTF8.GetBytes(canonical));
    }

    private static int Align4(int value) => checked((value + 3) & ~3);

    private static int ReadInt32(byte[] bytes, int offset)
    {
        RequireRange(bytes, offset, 4, "32-bit word");
        return BinaryPrimitives.ReadInt32LittleEndian(bytes.AsSpan(offset, 4));
    }

    private static void WriteInt32(byte[] bytes, int offset, int value)
    {
        RequireRange(bytes, offset, 4, "32-bit write");
        BinaryPrimitives.WriteInt32LittleEndian(bytes.AsSpan(offset, 4), value);
    }

    private static void RequireRange(byte[] bytes, int offset, int length, string label)
    {
        if (offset < 0 || length < 0 || (long)offset + length > bytes.Length)
            throw new InvalidDataException($"The ID65 {label} is outside its fixed model buffer.");
    }

    private static string Hash(ReadOnlySpan<byte> bytes) =>
        Convert.ToHexString(SHA256.HashData(bytes)).ToLowerInvariant();

    private static string HashFile(string path)
    {
        using FileStream input = File.OpenRead(path);
        return Convert.ToHexString(SHA256.HashData(input)).ToLowerInvariant();
    }

    private sealed record Component(string Name, int Offset, int ByteLength, byte[] Bytes);

    private sealed record ParsedSector(
        int Index,
        int Offset,
        int ByteLength,
        int LowDetailVertexCount,
        int LowDetailColorCount,
        int LowDetailFaceCount,
        int HighDetailVertexCount,
        int HighDetailColorCount,
        int HighDetailFaceCount,
        byte[] Bytes);

    private sealed record ParsedCollision(
        Component Component,
        int TriangleCount,
        int FlagCount,
        int TreeRelativeOffset,
        int BlocksRelativeOffset,
        int TrianglesRelativeOffset,
        int AssignmentsRelativeOffset,
        int FlagsRelativeOffset,
        int TreeOffset,
        int BlocksOffset,
        int TriangleOffset,
        int AssignmentsOffset,
        int FlagsOffset,
        int TreeCapacityBytes,
        int BlockCapacityBytes,
        byte[] TreeBytes,
        byte[] BlockBytes,
        byte[] TriangleBytes,
        byte[] AssignmentBytes,
        byte[] FlagBytes);

    private sealed record ParsedModel(
        byte[] Bytes,
        int UsedBytes,
        int ZeroTailBytes,
        IReadOnlyList<Component> Components,
        IReadOnlyList<ParsedSector> Sectors,
        ParsedCollision Collision,
        int PortalOffset);

    private sealed record CollisionCellBinding(
        UnusedLevel65FoundationCollisionCell Cell,
        int TreePointerByteOffset,
        int SourceBlockWordOffset,
        int[] OrderedTriangleIndexes);

    private sealed record NativeCollisionIndex(
        byte[] TreeBytes,
        byte[] BlockBytes,
        IReadOnlyDictionary<UnusedLevel65FoundationCollisionCell, CollisionCellBinding> Cells,
        int GroupStartCount,
        int UsedBlockBytes);

    private sealed record CollisionRepack(
        byte[] TreeBytes,
        byte[] BlockBytes,
        int UsedBlockBytes,
        int GroupStartCount,
        IReadOnlyList<UnusedLevel65FoundationCollisionCell> ChangedCells,
        NativeCollisionIndex Readback);

    private sealed record CollisionComposition(
        byte[] Bytes,
        byte[] TreeBytes,
        byte[] BlockBytes,
        int UsedBlockBytes,
        int CellCount,
        int GroupStartCount,
        IReadOnlyList<UnusedLevel65FoundationCollisionCell> ChangedCells,
        string TreeSha256,
        string BlocksSha256)
    {
        public static CollisionComposition ForFoundation(ParsedCollision collision)
        {
            NativeCollisionIndex index = DecodeCollisionIndex(
                collision.TreeBytes,
                collision.BlockBytes,
                FoundationUsedBlockBytes,
                collision.TriangleCount);
            return new(
                collision.Component.Bytes.ToArray(),
                collision.TreeBytes.ToArray(),
                collision.BlockBytes.ToArray(),
                FoundationUsedBlockBytes,
                index.Cells.Count,
                index.GroupStartCount,
                Array.Empty<UnusedLevel65FoundationCollisionCell>(),
                Hash(collision.TreeBytes),
                Hash(collision.BlockBytes));
        }
    }

    private sealed record ManifestValidation(
        IReadOnlyDictionary<string, UnusedLevel65FoundationTerrainPoint> Vertices,
        UnusedLevel65FoundationTerrainPoint[] OptionalPoints,
        CollisionRepack PreviewRepack,
        int SceneGrowthBytes,
        int CollisionGrowthBytes);

    private readonly record struct XyPoint(double X, double Y);

    private sealed record DecodedCollisionTriangle(
        int Index,
        IReadOnlyList<UnusedLevel65FoundationTerrainPoint> Points);
}
