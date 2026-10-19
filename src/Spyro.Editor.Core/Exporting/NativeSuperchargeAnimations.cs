using System.Buffers.Binary;
using System.Security.Cryptography;
using Spyro.Editor.Core.Analysis;
using Spyro.Editor.Core.Editing;
using Spyro.Editor.Core.Levels;
using Spyro.Editor.Core.Universal;

namespace Spyro.Editor.Core.Exporting;

/// <summary>
/// Spyro's supercharge animations for built levels with supercharge ground (native surface type 4), and for the game's
/// own levels given supercharge ground with Tile Type….
/// <para>Charging fast enough on type-4 ground puts Spyro in state 44 (0x80078AD0). The player-state routine takes his
/// animation from the state table at 0x8006C470 (state 44 -> animation 45, state 24 -> animation 25) and stores it at
/// 0x8003D0DC / 0x8003D10C. Animations 25 and 45 are level-local player records: exactly the seven retail levels with
/// supercharge ground (Magic Crafters, High Caves, Wizard Peak, Tree Tops, Lofty Castle, Haunted Towers, Twilight
/// Harbor) carry them, byte-identical, and no other level does. A built level's player stream is Town Square's
/// (18, 19, 29, 40); the stream loader (0x80013230) leaves slots 25 and 45 null, the frame read (0x80023B2C) then takes
/// its frame header from low memory, and Spyro's vertex routine (0x80024404) writes past the 1 KB scratchpad: a bus
/// error at 0x80024778 a frame after supercharge starts.</para>
/// <para>A scene with supercharge ground gets the two original records (Tree Tops' copies, checked against all seven
/// owners) appended to its player stream: 13,920 bytes of level memory. Animation 25 uses Spyro's standard face table;
/// animation 45 draws its own face and colour tables in the resident player package (geometry +0x30C10 / +0x32274),
/// whose textures (Spyro's sheet and the supercharge glow, page 0x1F u 96..127 v 64..95 with CLUT 0x7BFF) the level's
/// texture memory keeps: object imports treat them as taken (<see cref="ProtectedVram"/>) and the final level data is
/// checked against the original bytes (<see cref="RequireDestination"/>).</para>
/// <para>The other 28 original levels lack both records too (Spyro crashes the same way charging down Tile Type…
/// supercharge tiles in Artisans), so Create BIN adds them there as well (<see cref="MergeRetail"/>, through
/// <see cref="UniversalMobyExport.Apply"/>). Every original level's texture memory already holds the glow texture
/// exactly as Tree Tops does; five (Peacekeepers, Beast Makers, Misty Bog, Dream Weavers, Dark Passage) lack the game RAM
/// for the records in the 2 MB mode and are refused in plain words.</para>
/// </summary>
public sealed class NativeSuperchargeAnimations
{
    public static readonly IReadOnlyList<int> AnimationIds = Array.AsReadOnly(new[] { 25, 45 });
    /// <summary>Bytes the two records add to a level that lacks them.</summary>
    public const int ByteLength = 7612 + 6308;
    public const int DonorLevelId = 43;
    internal const string Record25Sha256 = "d5f15368f5fa3fa88022fa3c7cac77d13549dee0ba3c612cc926ee67c8663d52";
    internal const string Record45Sha256 = "d3cf9d6358ce272a5dd5c49c7591f702879a75545655619b861d62ba65335742";
    private static readonly int[] NativeOwners = [30, 32, 33, 43, 52, 53, 62];
    private const uint ExeBase = 0x8000F800;
    private const int Glow45FaceTable = 0x30C10, Glow45Colours = 0x32274, GeometryPackageOffset = 0xAA8;
    // Loader, frame reader and vertex routine, state -> animation table, and the state-44 animation writer.
    private static readonly (uint Pc, int Length, string Hash)[] NativeConsumers =
    [
        (0x80013230, 0x1B0, "0cca46f0e44c16b8adea088e3081637ec5b04a512dc2c8eac494f9d16732cefb"),
        (0x80023AC4, 0x1E2C, "5f151d5685850ef32ed2f55ea007cbbbb40b40dd8043231772e5c9c5da40e871"),
        (0x8006C470, 0x2D, "9f502dce2b68cb8497072c5c9c92d3f914710cdbbcbc9c25c86cbe29a4241e0b"),
        (0x8003D0B8, 0x60, "fb57e1678477aa2b8ef9b0fd55b49298ef998e7c60389c2c82d57e9bd7839ff1"),
    ];

    private readonly NativePlayerAnimationSource _source;
    private readonly IReadOnlyList<(int Offset, int Length)> _vram;
    private readonly byte[] _vramBytes;
    private NativeSuperchargeAnimations(NativePlayerAnimationSource source, IReadOnlyList<(int, int)> vram, byte[] vramBytes)
    { _source = source; _vram = vram; _vramBytes = vramBytes; }

    /// <summary>The scene has supercharge ground (painted faces or collision-only faces).</summary>
    public static bool Needed(SceneDocument document) =>
        document.GroundKinds().Any(k => k == AuthoredSurfaceKind.Supercharge);

    /// <summary>The original levels whose player stream already carries both records: they need nothing added.</summary>
    public static bool CarriedBy(int levelId) => NativeOwners.Contains(levelId);

    /// <summary>One of the game's own levels whose saved terrain edits give a tile supercharge ground (Tile Type…, or a
    /// texture that brought its donor's kind) and whose player stream lacks the records.</summary>
    public static bool NeededByTerrainEdits(int levelId, string terrainEditsPath) =>
        !CarriedBy(levelId) && UniversalLiquidSplashes.KindsInTerrainEdits(terrainEditsPath).Any(k => k.Type == AuthoredSurfaceKind.Supercharge.Type);

    /// <summary>Texture-memory bytes (offsets in the level's 512 KB texture upload) animation 45 draws from.</summary>
    public IEnumerable<(long Offset, int Length)> ProtectedVram => _vram.Select(r => ((long)r.Offset, r.Length));

    /// <summary>The plain refusal when the records do not fit the level's memory.</summary>
    public static string NoRoomMessage(string levelName, long shortfall) =>
        $"{levelName} has supercharge ground, which needs Spyro's supercharge animations ({ByteLength:N0} bytes of level memory), " +
        $"and the level is {shortfall:N0} bytes short of room for them. Remove some geometry or objects, or change the supercharge ground to another kind.";

    /// <summary>The plain refusal for one of the game's own levels (supercharge tiles given with Tile Type…).</summary>
    public static string RetailNoRoomMessage(string levelName, long shortfall) =>
        $"{levelName} has supercharge ground, which needs Spyro's supercharge animations, and {levelName} has none of its own. " +
        $"Adding them takes {ByteLength:N0} bytes of level memory, and the level is {shortfall:N0} bytes short of room for them. " +
        "Change those tiles to another kind with Tile Type…, remove objects you added to the level, or choose Emulator only (8 MB RAM) under Game Settings › Memory.";

    /// <summary>Bytes of game RAM one of the game's own levels is short of for the two records in the current memory mode
    /// (0 when they fit or the level carries them), read from the unchanged original disc. Other edits to the level are
    /// not counted; Create BIN checks the finished level again.</summary>
    public static async Task<long> RetailShortfallAsync(string originalUSA, LevelDefinition level, CancellationToken token = default)
    {
        if (CarriedBy(level.LevelId))
            return 0;
        var animations = await CaptureAsync(originalUSA, token);
        var disc = UniversalDiscImage.Read(originalUSA);
        var data = new UniversalLevelData(disc.EntryBytes(level.SourceWadEntry));
        uint copy = new UniversalOverlay(disc.EntryBytes(level.SourceWadEntry - 1), UniversalExeBindings.Read(disc.Exe, level.LevelId)).CopyBuffer;
        var merged = animations.Merge(data);
        return GameRam.Exceeds(merged, copy, data, copy) ? GameRam.Shortfall(merged, copy) : 0;
    }

    /// <summary>Certify the original records, the resident tables animation 45 reads and the native consumers.</summary>
    internal static async Task<NativeSuperchargeAnimations> CaptureAsync(string originalUSA, CancellationToken token = default)
    {
        var source = await NativePlayerAnimationSource.CaptureAsync(originalUSA, token);
        string path = Path.GetFullPath(originalUSA);
        await using FileStream image = new(path, FileMode.Open, FileAccess.Read, FileShare.Read);
        DiscLayout layout = DiscImage.DetectLayout(path);
        byte[] executable = DiscImage.ReadFileBytes(image, layout, 53875, 0, 0x66000);
        byte[] package = DiscImage.ReadFileBytes(image, layout, 37, NativePlayerAnimationSource.SharedPlayerPackageWadOffset,
            NativePlayerAnimationSource.SharedPlayerPackageByteLength);
        var donor = source.Levels.Single(l => l.LevelId == DonorLevelId);
        byte[] donorData = DiscImage.ReadFileBytes(image, layout, 37, donor.DataWadOffset, donor.DataByteLength);
        if (Hash(package) != source.SharedPlayerPackageSha256 || Hash(donorData) != donor.DataSha256)
            throw new InvalidDataException("Supercharge animation sources changed after the player-animation capture.");
        return Certify(source, executable, package, donorData);
    }

    internal static NativeSuperchargeAnimations Certify(NativePlayerAnimationSource source, byte[] executable,
        byte[] package, byte[] donorData)
    {
        if (source.SourceImageSha256 != NativePlayerAnimationSource.OriginalImageSha256 ||
            source.SharedPlayerPackageSha256 != "46f570bf0ad2d433f8b42874ae40a3e5d7155d7206dddd1cd08a2d4be16e0e76" ||
            AnimationIds.Any(source.SharedAnimationIds.Contains))
            throw new InvalidDataException("Supercharge animations lost their original local/shared player owners.");
        if (executable.Length != 0x66000)
            throw new InvalidDataException("Supercharge animations need the complete original executable.");
        foreach (var (pc, length, hash) in NativeConsumers)
            if (Hash(executable.AsSpan((int)(pc - ExeBase), length)) != hash)
                throw new InvalidDataException("Supercharge animations lost their native loader, renderer or state consumer.");
        if (executable[(int)(0x8006C470 - ExeBase) + 44] != 45 || executable[(int)(0x8006C470 - ExeBase) + 24] != 25)
            throw new InvalidDataException("Supercharge states no longer select animations 25 and 45.");
        var standard = source.Levels.Single(l => l.LevelId == 10).Stream.Records.Single(r => r.AnimationId == 18).CopyRecord();
        foreach (int id in AnimationIds)
        {
            var owners = source.Levels.Where(l => l.Stream.Records.Any(r => r.AnimationId == id)).Select(l => l.LevelId).Order().ToArray();
            if (!owners.SequenceEqual(NativeOwners))
                throw new InvalidDataException($"Spyro animation {id} has a different retail owner population than the supercharge levels.");
            foreach (int level in owners)
            {
                var record = source.Levels.Single(l => l.LevelId == level).Stream.Records.Single(r => r.AnimationId == id);
                byte[] bytes = record.CopyRecord();
                if (record.Sha256 != (id == 25 ? Record25Sha256 : Record45Sha256) || Hash(bytes) != record.Sha256 ||
                    record.ByteLength != (id == 25 ? 7612 : 6308) || record.FrameCount != (id == 25 ? 18 : 13) ||
                    record.Frames.Any(f => f.UpperByte != 0xFF))
                    throw new InvalidDataException($"Spyro animation {id} differs from its original supercharge record.");
                if (id == 25 && !bytes.AsSpan(0x0A, 0x22).SequenceEqual(standard.AsSpan(0x0A, 0x22)))
                    throw new InvalidDataException("Spyro animation 25 no longer uses Spyro's standard faces and colours.");
                // Animation 45: its own face table and colour table in the resident geometry, nothing local.
                if (id == 45 && (Word(bytes, 8 + 0x14) != Glow45FaceTable || Word(bytes, 8 + 0x18) != Glow45Colours ||
                    Word(bytes, 8 + 0x1C) != 0 || bytes[8 + 2] != 0x40))
                    throw new InvalidDataException("Spyro animation 45 names other resident face or colour tables.");
            }
        }
        if (package.Length != NativePlayerAnimationSource.SharedPlayerPackageByteLength ||
            Hash(package) != source.SharedPlayerPackageSha256 || BinaryPrimitives.ReadInt32LittleEndian(package.AsSpan(0x34)) != GeometryPackageOffset)
            throw new InvalidDataException("Supercharge animations need the unchanged resident player package.");
        // 45's face table (length word + commands) runs into its 64-colour table, which ends the package.
        int table = GeometryPackageOffset + Glow45FaceTable, tableLength = BinaryPrimitives.ReadInt32LittleEndian(package.AsSpan(table));
        if (table + 4 + tableLength != GeometryPackageOffset + Glow45Colours || GeometryPackageOffset + Glow45Colours + 64 * 4 != package.Length)
            throw new InvalidDataException("Spyro animation 45's face and colour tables changed in the resident player package.");
        // Every textured command's rectangle and palette, in the level's texture upload, from the donor's own copy.
        var merged = TextureRanges(package, table, out int commands);
        if (commands != 278) throw new InvalidDataException("Spyro animation 45's textured faces changed.");
        int vram = new UniversalLevelData(donorData).Subfiles()[0].Offset;
        byte[] expected = merged.SelectMany(r => donorData.AsSpan(vram + r.Item1, r.Item2).ToArray()).ToArray();
        return new(source, merged, expected);
    }

    /// <summary>The texture-upload bytes (pixel rows and palettes, merged and sorted) a model face table draws from.
    /// <paramref name="table"/> is the table's length word; commands follow the native renderer grammar
    /// (<see cref="NativeMobyModelTextureLayout"/>).</summary>
    internal static IReadOnlyList<(int Offset, int Length)> TextureRanges(ReadOnlySpan<byte> package, int table, out int commands)
    {
        if (table < 0 || table % 4 != 0 || table + 4L > package.Length)
            throw new InvalidDataException("Face table lies outside its package.");
        int tableLength = BinaryPrimitives.ReadInt32LittleEndian(package[table..]);
        if (tableLength < 0 || tableLength % 4 != 0 || table + 4L + tableLength > package.Length)
            throw new InvalidDataException("Face table overruns its package.");
        SortedDictionary<int, int> ranges = [];
        commands = 0;
        for (int cursor = table + 4, end = table + 4 + tableLength; cursor < end;)
        {
            if (cursor + 8 > end) throw new InvalidDataException("Face table command overruns its owner.");
            uint command = BinaryPrimitives.ReadUInt32LittleEndian(package[cursor..]); int size, field;
            if ((command & 0x80000000) == 0) { size = (command & 2) != 0 ? 20 : 8; field = (command & 2) != 0 ? cursor + 8 : -1; }
            else if ((command & 4) != 0) { size = 20; field = cursor + 8; }
            else { size = (command & 2) != 0 ? 24 : 12; field = (command & 2) != 0 ? cursor + 12 : -1; }
            if (cursor + size > end) throw new InvalidDataException("Face table command overruns its owner.");
            if (field >= 0)
            {
                commands++;
                var footprint = NativeId65PlayerTextureOwners.DecodeFootprint(package.Slice(field, 12));
                foreach (var r in footprint.PixelRows.Append(footprint.Palette))
                    ranges[r.VramRelativeOffset] = Math.Max(ranges.GetValueOrDefault(r.VramRelativeOffset), r.ByteLength);
            }
            cursor += size;
        }
        List<(int, int)> merged = [];
        foreach (var (offset, length) in ranges)
        {
            if (merged.Count > 0 && offset <= merged[^1].Item1 + merged[^1].Item2)
                merged[^1] = (merged[^1].Item1, Math.Max(merged[^1].Item2, offset + length - merged[^1].Item1));
            else merged.Add((offset, length));
        }
        return merged.AsReadOnly();
    }

    /// <summary>The level data with both records in its player stream (unchanged when it already has them).</summary>
    internal UniversalLevelData Merge(UniversalLevelData body)
    {
        var layout = _source.MergeCaptured(body.Bytes, DonorLevelId, [.. AnimationIds]);
        if (layout.AddedIds.Except(AnimationIds).Any() || layout.ExactGrowth != layout.AddedIds.Sum(id => id == 25 ? 7612 : 6308))
            throw new InvalidDataException("Supercharge animation merge added unexpected player records.");
        var merged = new UniversalLevelData(layout.CopyFinalBytes());
        RequireRecords(merged.Bytes);
        return merged;
    }

    /// <summary>One of the game's own levels with both records in its player stream (unchanged when it carries them),
    /// refused in plain words when its game RAM in the current memory mode cannot take them (a level already past the
    /// margin keeps its own end, as for object imports: <see cref="GameRam.Exceeds(UniversalLevelData, uint, UniversalLevelData, uint)"/>).</summary>
    internal UniversalLevelData MergeRetail(UniversalLevelData data, uint copyBuffer, string levelName)
    {
        var merged = Merge(data);
        if (GameRam.Exceeds(merged, copyBuffer, data, copyBuffer))
            throw new InvalidDataException(RetailNoRoomMessage(levelName, GameRam.Shortfall(merged, copyBuffer)));
        RequireDestination(merged.Bytes);
        return merged;
    }

    /// <summary>Final level data: both records present and exact, and animation 45's texture bytes unchanged.</summary>
    internal void RequireDestination(ReadOnlySpan<byte> levelData)
    {
        byte[] bytes = levelData.ToArray();
        RequireRecords(bytes);
        var vram = new UniversalLevelData(bytes).Vram();
        int at = 0;
        foreach (var (offset, length) in _vram)
        {
            if (!vram.Slice(offset, length).SequenceEqual(_vramBytes.AsSpan(at, length)))
                throw new InvalidDataException("Another texture took the place of Spyro's supercharge textures in this level. Remove the most recently added object and try again.");
            at += length;
        }
    }

    private void RequireRecords(byte[] levelData)
    {
        // The exact source merge again, as a readback: nothing may be added, every record is re-checked.
        var check = _source.MergeCaptured(levelData, DonorLevelId, [.. AnimationIds]);
        if (check.AddedIds.Count != 0 || check.ExactGrowth != 0 || !check.CopyFinalBytes().AsSpan().SequenceEqual(levelData))
            throw new InvalidDataException("Spyro's supercharge animations are missing from the level's player stream.");
    }

    private static string Hash(ReadOnlySpan<byte> bytes) => Convert.ToHexStringLower(SHA256.HashData(bytes));
    private static uint Word(ReadOnlySpan<byte> bytes, int offset) => BinaryPrimitives.ReadUInt32LittleEndian(bytes[offset..]);
}
