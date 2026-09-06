using System.Buffers.Binary;
using System.Globalization;
using System.Security.Cryptography;
using Spyro.Editor.Core.Exporting;
using Spyro.Editor.Core.Levels;

namespace Spyro.Editor.Core.Analysis;

public enum NativeMobyPropertiesLocation { Zero, InsideComponent, ComponentEnd, OutsideComponent }
public enum NativeMobyLocalModelStatus { Present, Absent }

/// <summary>A source row, never a portable instance schema. Names and caches cannot erase variants.</summary>
public sealed class NativeMobySourceRow
{
    private readonly byte[] _record;
    internal NativeMobySourceRow(ReadOnlySpan<byte> record, int index, long wadOffset, int levelId,
        string dataHash, bool hasRoot, NativeMobyPropertiesLocation propertiesLocation)
    {
        _record = record.ToArray(); TrueIndex = index; RecordWadOffset = wadOffset;
        RecordSha256 = NativeMobySourceInventory.Hash(record);
        SourceVariantId = $"{NativeMobySourceInventory.OriginalImageSha256}:level-{levelId}:data-{dataHash}:row-{index}:{RecordSha256}";
        HasLocalModelRegistration = hasRoot; PropertiesLocation = propertiesLocation;
    }
    public int TrueIndex { get; }
    public long RecordWadOffset { get; }
    public string RecordSha256 { get; }
    public string SourceVariantId { get; }
    public ushort ActorId => BinaryPrimitives.ReadUInt16LittleEndian(_record.AsSpan(0x36));
    public byte RenderRadius => _record[0x50];
    public byte InitialState => _record[0x51];
    public uint PropertiesPointer => BinaryPrimitives.ReadUInt32LittleEndian(_record);
    public uint AssociatedWord => BinaryPrimitives.ReadUInt32LittleEndian(_record.AsSpan(8));
    public bool HasLocalModelRegistration { get; }
    public NativeMobyPropertiesLocation PropertiesLocation { get; }
    public byte[] CopyRecord() => _record.ToArray();
}

/// <summary>The root is owned by the model subfile, but this does not establish its package extent.</summary>
public sealed record NativeMobySourceRegistration(int Slot, ushort ActorId, uint RootDataOffset);

public sealed class NativeMobySourceLevel
{
    internal NativeMobySourceLevel(LevelDefinition definition, long dataWadOffset, int dataLength, string dataHash,
        int sceneOffset, int sceneLength, int mobyOffset, int mobyLength, int arenaLength, int propertiesOffset,
        int propertiesLength, IEnumerable<NativeMobySourceRow> rows, IEnumerable<NativeMobySourceRegistration> roots)
    {
        LevelKey = LevelCatalog.NormalizeKey(definition.Key); DisplayName = definition.DisplayName;
        LevelId = definition.LevelId; DataWadEntry = definition.SourceWadEntry;
        DataWadOffset = dataWadOffset; SourceDataByteLength = dataLength; SourceDataSha256 = dataHash;
        SceneDataOffset = sceneOffset; SceneByteLength = sceneLength; MobyComponentSceneOffset = mobyOffset;
        MobyComponentByteLength = mobyLength; ReservedMobyArenaByteLength = arenaLength;
        PropertiesComponentSceneOffset = propertiesOffset; PropertiesComponentByteLength = propertiesLength;
        Rows = Array.AsReadOnly(rows.ToArray()); Registrations = Array.AsReadOnly(roots.ToArray());
    }
    public string LevelKey { get; }
    public string DisplayName { get; }
    public int LevelId { get; }
    public int DataWadEntry { get; }
    public long DataWadOffset { get; }
    public int SourceDataByteLength { get; }
    public string SourceDataSha256 { get; }
    public int SceneDataOffset { get; }
    public int SceneByteLength { get; }
    public int MobyComponentSceneOffset { get; }
    public int MobyComponentByteLength { get; }
    public int ReservedMobyArenaByteLength { get; }
    public int PropertiesComponentSceneOffset { get; }
    public int PropertiesComponentByteLength { get; }
    public IReadOnlyList<NativeMobySourceRow> Rows { get; }
    public IReadOnlyList<NativeMobySourceRegistration> Registrations { get; }
    public bool NativeImportApproved => false;
}

public sealed record NativeMobySourceClass(ushort ActorId, int PlacedRowCount, int RegistrationCount,
    IReadOnlyList<string> PlacedLevelKeys, IReadOnlyList<string> RegisteredLevelKeys);

public sealed record NativeMobyDestinationDiscovery(ushort ActorId, string TargetLevelKey,
    NativeMobyLocalModelStatus LocalModelStatus, uint? LocalRootDataOffset, int NativePlacedRowCount)
{
    public bool NativeImportApproved => false;
    public string Meaning => "Discovery only. A local root is not proof of equivalent models, behavior, animation, textures, sound, " +
        "player dependencies, properties ownership, routes, persistence or memory capacity. An absent root is not proof " +
        "of absent behavior: global, rootless and special-rendered actors need separate source schemas.";
}

/// <summary>
/// Read-only, complete placed-row/local-registration discovery for the exact original USA image.
/// This intentionally includes controllers, unknown classes and resources without placed rows.
/// It does not discover every dynamically emitted/global actor or grant export authority.
/// Only the public Capture route issues source-bound inventories; synthetic decoding stays internal.
/// </summary>
public sealed class NativeMobySourceInventory
{
    public const string OriginalImageSha256 = "fc866b2a02e010a6658f8af2de28bb3001eb33513e5924af014e35643c6dee37";
    public const long OriginalImageByteLength = 661547040;
    private const int RecordBytes = 0x58, MaximumEntryBytes = 0x800000;
    private NativeMobySourceInventory(IEnumerable<NativeMobySourceLevel> levels)
    {
        Levels = Array.AsReadOnly(levels.OrderBy(l => l.LevelId).ToArray());
        ushort[] classes = Levels.SelectMany(l => l.Rows.Select(r => r.ActorId)
            .Concat(l.Registrations.Select(r => r.ActorId))).Distinct().Order().ToArray();
        Classes = Array.AsReadOnly(classes.Select(id => new NativeMobySourceClass(id,
            Levels.Sum(l => l.Rows.Count(r => r.ActorId == id)), Levels.Sum(l => l.Registrations.Count(r => r.ActorId == id)),
            Array.AsReadOnly(Levels.Where(l => l.Rows.Any(r => r.ActorId == id)).Select(l => l.LevelKey).ToArray()),
            Array.AsReadOnly(Levels.Where(l => l.Registrations.Any(r => r.ActorId == id)).Select(l => l.LevelKey).ToArray()))).ToArray());
    }
    public string SourceImageSha256 => OriginalImageSha256;
    public string Scope => "All 35 retail level placed-row tables and local model registrations in the original USA disc; not a universal import approval or exhaustive global/dynamic actor inventory.";
    public IReadOnlyList<NativeMobySourceLevel> Levels { get; }
    public IReadOnlyList<NativeMobySourceClass> Classes { get; }

    public NativeMobyDestinationDiscovery DescribeDestination(ushort actorId, string targetKey)
    {
        if (!Classes.Any(c => c.ActorId == actorId))
            throw new InvalidDataException($"Class 0x{actorId:X4} has not been discovered in these native source tables.");
        var target = Levels.SingleOrDefault(l => l.LevelKey == LevelCatalog.NormalizeKey(targetKey))
            ?? throw new InvalidDataException("Destination is not an inspected retail level; new slots require independent native ownership.");
        var root = target.Registrations.SingleOrDefault(r => r.ActorId == actorId);
        return new(actorId, target.LevelKey, root == null ? NativeMobyLocalModelStatus.Absent : NativeMobyLocalModelStatus.Present,
            root?.RootDataOffset, target.Rows.Count(r => r.ActorId == actorId));
    }

    public static NativeMobySourceInventory Capture(string sourceImagePath, LevelCatalog catalog)
    {
        ArgumentNullException.ThrowIfNull(catalog);
        // Source table locations/counts remain independently checked below. Metadata may label
        // discovered records but cannot redirect the native table or omit inconvenient levels.
        var definitions = catalog.Levels.OrderBy(l => l.SourceWadEntry).ToArray();
        if (definitions.Length != 35 || definitions.Select(l => LevelCatalog.NormalizeKey(l.Key)).Distinct().Count() != 35)
            throw new InvalidDataException("Native game-wide discovery requires exactly 35 distinct retail catalog levels.");
        for (int i = 0; i < definitions.Length; i++)
            if (definitions[i].SourceWadEntry != 10 + i * 2 || definitions[i].LevelId != (i / 6 + 1) * 10 + i % 6)
                throw new InvalidDataException("Catalog level IDs/entries differ from the original retail slot ownership.");
        string path = Path.GetFullPath(sourceImagePath);
        using FileStream image = new(path, FileMode.Open, FileAccess.Read, FileShare.Read);
        RequireOriginal(image);
        DiscLayout layout = DiscImage.DetectLayout(path);
        if (layout.SectorSize != 2352 || layout.UserOffset != 24)
            throw new InvalidDataException("Original USA source must use MODE2/2352 geometry.");
        var wad = DiscImage.FindRootFileRecord(image, layout, n => n.Equals("WAD.WAD", StringComparison.OrdinalIgnoreCase));
        if (wad.Lba != 37 || wad.Size != 0x6927000)
            throw new InvalidDataException("Native inventory WAD ISO ownership differs from its source certificate.");
        byte[] header = DiscImage.ReadFileBytes(image, layout, wad.Lba, 0, 2048);
        List<NativeMobySourceLevel> levels = [];
        foreach (var definition in definitions)
        {
            int field = definition.SourceWadEntry * 8;
            uint offset = Word(header, field), length = Word(header, field + 4);
            if (offset < 2048 || offset % 2048 != 0 || length < 2048 || length > MaximumEntryBytes ||
                length % 2048 != 0 || offset + (long)length > wad.Size)
                throw new InvalidDataException("Native inventory entry exceeds its bounded source owner.");
            byte[] data = DiscImage.ReadFileBytes(image, layout, wad.Lba, offset, (int)length);
            levels.Add(DecodeLevel(data, definition, offset));
        }
        RequireOriginal(image); // No source identity borrowed across a changed read session.
        return new(levels);
    }

    internal static NativeMobySourceLevel DecodeLevel(ReadOnlySpan<byte> data, LevelDefinition definition, long dataWadOffset)
    {
        if (data.Length < 0x1D0 || data.Length > MaximumEntryBytes || dataWadOffset < 0 ||
            dataWadOffset > long.MaxValue - data.Length || definition == null)
            throw new InvalidDataException("Native Moby source level has no bounded header/owner.");
        int modelOffset = Integer(data, 0x10), modelLength = Integer(data, 0x14);
        int sceneOffset = Integer(data, 0x18), sceneLength = Integer(data, 0x1C);
        Range(modelOffset, modelLength, data.Length, 4);
        Range(sceneOffset, sceneLength, data.Length, 0xC8);
        if (modelOffset < 0x1D0 || modelOffset + (long)modelLength > sceneOffset)
            throw new InvalidDataException("Native model and scene owners overlap or enter the header.");
        List<NativeMobySourceRegistration> roots = [];
        HashSet<ushort> classes = [];
        bool terminated = false;
        uint previous = 0;
        for (int i = 0; i < 64; i++)
        {
            uint root = Word(data, 0x50 + i * 4);
            ushort actor = BinaryPrimitives.ReadUInt16LittleEndian(data[(0x150 + i * 2)..]);
            if (root == 0)
            {
                terminated = true;
                if (actor != 0) throw new InvalidDataException("Native null root retains an unexplained class ID.");
                continue;
            }
            if (terminated || root % 4 != 0 || root <= previous || root < modelOffset ||
                root + 4L > modelOffset + (long)modelLength || !classes.Add(actor))
                throw new InvalidDataException("Native registration is aliased, unordered or outside its model owner.");
            roots.Add(new(i, actor, root)); previous = root;
        }
        ReadOnlySpan<byte> scene = data.Slice(sceneOffset, sceneLength);
        int cursor = 0x88;
        // Exact-retail scene grammar, cross-checked against every native row table.
        // Skip declared owners, never scan zero runs or count-like values to locate rows.
        for (int i = 0; i < 7; i++) cursor = checked(cursor + ComponentLength(scene, cursor, 8));
        int mobyOffset = cursor, mobyLength = ComponentLength(scene, cursor, 8), count = Integer(scene, cursor + 4);
        if (count > 4096 || 8L + count * (long)RecordBytes > mobyLength)
            throw new InvalidDataException("Native placed rows exceed their declared Moby component.");
        long relativeTable = sceneOffset + (long)cursor + 8;
        if (definition.SourceRecordCount != count || Offset(definition.SourceTableRelativeOffset) != relativeTable ||
            Offset(definition.SourceTableWadOffset) != dataWadOffset + relativeTable)
            throw new InvalidDataException("Catalog row identity disagrees with the native scene component chain.");
        int propertiesOffset = checked(cursor + mobyLength), propertiesLength = ComponentLength(scene, propertiesOffset, 4);
        int propertiesStart = propertiesOffset + 4, propertiesEnd = propertiesOffset + propertiesLength;
        string dataHash = Hash(data);
        List<NativeMobySourceRow> rows = [];
        for (int i = 0; i < count; i++)
        {
            int rowOffset = checked(cursor + 8 + i * RecordBytes);
            ReadOnlySpan<byte> row = scene.Slice(rowOffset, RecordBytes);
            uint pointer = Word(row, 0);
            NativeMobyPropertiesLocation location = pointer == 0 ? NativeMobyPropertiesLocation.Zero :
                pointer == propertiesEnd ? NativeMobyPropertiesLocation.ComponentEnd :
                pointer >= propertiesStart && pointer < propertiesEnd ? NativeMobyPropertiesLocation.InsideComponent :
                NativeMobyPropertiesLocation.OutsideComponent;
            rows.Add(new(row, i, dataWadOffset + sceneOffset + rowOffset, definition.LevelId, dataHash,
                classes.Contains(BinaryPrimitives.ReadUInt16LittleEndian(row[0x36..])), location));
        }
        return new(definition, dataWadOffset, data.Length, dataHash, sceneOffset, sceneLength, mobyOffset, mobyLength,
            checked(mobyLength - 8 - count * RecordBytes), propertiesOffset, propertiesLength, rows, roots);
    }

    private static void RequireOriginal(FileStream stream)
    {
        stream.Position = 0;
        if (stream.Length != OriginalImageByteLength || Convert.ToHexStringLower(SHA256.HashData(stream)) != OriginalImageSha256)
            throw new InvalidDataException("Native source inventory requires the exact unchanged original USA disc image.");
    }
    private static long Offset(string value)
    {
        string text = value?.Trim() ?? "";
        bool hex = text.StartsWith("0x", StringComparison.OrdinalIgnoreCase);
        if (!long.TryParse(hex ? text[2..] : text, hex ? NumberStyles.AllowHexSpecifier : NumberStyles.None,
            CultureInfo.InvariantCulture, out long result) || result < 0)
            throw new InvalidDataException("Catalog contains an invalid native table offset.");
        return result;
    }
    private static int ComponentLength(ReadOnlySpan<byte> bytes, int offset, int minimum)
    {
        Range(offset, 4, bytes.Length, 4);
        int length = Integer(bytes, offset);
        Range(offset, length, bytes.Length, minimum);
        return length;
    }
    private static void Range(int offset, int length, int ownerLength, int minimum)
    {
        if (offset < 0 || length < minimum || (offset | length) % 4 != 0 || offset + (long)length > ownerLength)
            throw new InvalidDataException("Native inventory component is truncated, misaligned or outside its source owner.");
    }
    private static int Integer(ReadOnlySpan<byte> bytes, int offset)
    {
        uint value = Word(bytes, offset);
        return value <= int.MaxValue ? (int)value : throw new InvalidDataException("Native inventory value exceeds bounded integer geometry.");
    }
    private static uint Word(ReadOnlySpan<byte> bytes, int offset) => BinaryPrimitives.ReadUInt32LittleEndian(bytes[offset..]);
    internal static string Hash(ReadOnlySpan<byte> bytes) => Convert.ToHexStringLower(SHA256.HashData(bytes));
}
