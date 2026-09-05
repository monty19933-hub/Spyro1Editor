using System.Buffers.Binary;
using System.Reflection;
using System.Runtime.ExceptionServices;
using System.Text;
using Spyro.Editor.Core.Exporting;
using Spyro.Editor.Core.Levels;

const int TableOffset = 0x5FC38;
string temporaryRoot = Path.Combine(Path.GetTempPath(), $"spyro-treasure-export-{Guid.NewGuid():N}");
Directory.CreateDirectory(temporaryRoot);
List<string> failures = [];
int checks = 0;
try
{
    Require(DiscImage.ConvertFileOffsetToImageOffset(new DiscLayout(2352, 24, 0, 0), 53875, TableOffset) == 0x7945FB0,
        "Executable-relative table does not match the original USA raw2352 location.");
    foreach ((int size, int offset) in new[] { (2352, 24), (2336, 8), (2048, 0) })
    {
        foreach (int exeLba in new[] { 20, 28 })
            Run($"{size} executable LBA {exeLba}", () => CheckExport(size, offset, exeLba));
    }
    if (failures.Count != 0) throw new InvalidOperationException($"{failures.Count}/{checks} failed:\n" + string.Join('\n', failures));
    Console.WriteLine($"MOBY_TREASURE_EXPORT_SMOKE_OK checks={checks}; synthetic ISO/executable, native level slot, relocated extent, exact before/after, source preimage and integrity");
}
finally { Directory.Delete(temporaryRoot, recursive: true); }

void Run(string name, Action action)
{
    checks++;
    try { action(); }
    catch (Exception ex) { failures.Add($"{name}: {ex.Message}"); }
}

void CheckExport(int size, int offset, int exeLba)
{
    string path = Path.Combine(temporaryRoot, $"{Guid.NewGuid():N}.bin");
    byte[] original = BuildFixture(size, offset, exeLba);
    File.WriteAllBytes(path, original);
    LevelDefinition artisans = new() { Key = "artisans", DisplayName = "Artisans", LevelId = 10 };
    LevelDefinition stonehill = new() { Key = "stonehill", DisplayName = "Stone Hill", LevelId = 11 };
    // Reversed catalog proves a file's ordering must not choose the native table slot.
    LevelCatalog catalog = new([stonehill, artisans]);
    List<MobySourcePatch> patches = [];
    using FileStream stream = File.Open(path, FileMode.Open, FileAccess.ReadWrite, FileShare.Read);
    Invoke("AddTreasureTotalPatch", stream, catalog, artisans, 5, patches, new List<string>());
    Require(patches.Count == 1, "Treasure total edit was skipped because the old physical location is outside this image.");
    MobySourcePatch patch = patches[0];
    DiscLayout layout = DiscImage.DetectLayout(path);
    Require(patch.WadRelativeOffset == "exe:0x8006F438", "Treasure patch has no canonical executable runtime address.");
    Require(patch.BeforeHexPreview == "64 00" && patch.AfterHexPreview == "69 00", "Treasure patch chose the wrong native level slot or preimage.");
    Require(patch.ImageOffset == $"0x{DiscImage.ConvertFileOffsetToImageOffset(layout, exeLba, TableOffset):X}",
        "Treasure patch physical location did not follow the live executable extent/layout.");
    Invoke("ApplySourcePatch", stream, layout, patch);
    stream.Flush();
    Require(DiscImage.ReadFileBytes(stream, layout, exeLba, TableOffset, 4).AsSpan().SequenceEqual(new byte[] { 105, 0, 200, 0 }),
        "Treasure patch did not preserve its neighboring level slot.");
    byte[] after = File.ReadAllBytes(path);
    long touchedSector = exeLba + TableOffset / 2048;
    for (int index = 0; index < original.Length; index++)
    {
        bool payload = index == touchedSector * size + offset + TableOffset % 2048 ||
                       index == touchedSector * size + offset + TableOffset % 2048 + 1;
        bool parity = size != 2048 && index >= touchedSector * size + offset + 2048 && index < (touchedSector + 1) * size;
        Require(payload || parity || original[index] == after[index], "Treasure export changed an unrelated physical byte.");
    }
    if (size == 2352)
        RawMode2Form1SectorIntegrity.VerifyAbsoluteSectors(stream, layout, [(checked((int)touchedSector), 1)]);
    else if (size == 2336)
    {
        byte[] sector = after.AsSpan(checked((int)touchedSector * size), size).ToArray();
        RawMode2Form1SectorIntegrity.RebuildLogicalWriteSector(sector, layout, touchedSector);
        Require(sector.AsSpan().SequenceEqual(after.AsSpan(checked((int)touchedSector * size), size)), "Headerless sector parity is stale.");
    }
    bool staleRejected = false;
    try { Invoke("ApplySourcePatch", stream, layout, patch); }
    catch (InvalidDataException) { staleRejected = true; }
    Require(staleRejected && File.ReadAllBytes(path).AsSpan().SequenceEqual(after), "Stale plan preimage was not rejected before mutation.");
    bool physicalRejected = false;
    try { Invoke("ApplySourcePatch", stream, layout, patch with { WadRelativeOffset = "" }); }
    catch (InvalidDataException) { physicalRejected = true; }
    Require(physicalRejected && File.ReadAllBytes(path).AsSpan().SequenceEqual(after), "Legacy physical-only patch escaped logical addressing.");
}

static void Invoke(string method, params object[] args)
{
    MethodInfo target = typeof(MobySourcePatchExporter).GetMethod(method, BindingFlags.Static | BindingFlags.NonPublic)
        ?? throw new InvalidOperationException($"Missing {method}.");
    try { target.Invoke(null, args); }
    catch (TargetInvocationException ex) when (ex.InnerException != null) { ExceptionDispatchInfo.Capture(ex.InnerException).Throw(); }
}

static byte[] BuildFixture(int size, int offset, int exeLba)
{
    const int exeLength = 0x60000;
    byte[] logical = new byte[(exeLba + exeLength / 2048 + 2) * 2048];
    Span<byte> pvd = logical.AsSpan(16 * 2048, 2048);
    pvd[0] = 1;
    Encoding.ASCII.GetBytes("CD001").CopyTo(pvd[1..]);
    pvd[6] = 1;
    BinaryPrimitives.WriteUInt16LittleEndian(pvd[128..], 2048);
    BinaryPrimitives.WriteUInt16BigEndian(pvd[130..], 2048);
    pvd[156] = 34;
    BinaryPrimitives.WriteInt32LittleEndian(pvd[158..], 18);
    BinaryPrimitives.WriteInt32BigEndian(pvd[162..], 18);
    BinaryPrimitives.WriteInt32LittleEndian(pvd[166..], 2048);
    BinaryPrimitives.WriteInt32BigEndian(pvd[170..], 2048);
    pvd[188] = 1;
    Span<byte> record = logical.AsSpan(18 * 2048, 2048);
    byte[] name = Encoding.ASCII.GetBytes("SCUS_942.28;1");
    record[0] = (byte)(33 + name.Length + (name.Length % 2 == 0 ? 1 : 0));
    BinaryPrimitives.WriteInt32LittleEndian(record[2..], exeLba);
    BinaryPrimitives.WriteInt32BigEndian(record[6..], exeLba);
    BinaryPrimitives.WriteInt32LittleEndian(record[10..], exeLength);
    BinaryPrimitives.WriteInt32BigEndian(record[14..], exeLength);
    record[32] = (byte)name.Length;
    name.CopyTo(record[33..]);
    Span<byte> exe = logical.AsSpan(exeLba * 2048, exeLength);
    Encoding.ASCII.GetBytes("PS-X EXE").CopyTo(exe);
    BinaryPrimitives.WriteUInt32LittleEndian(exe[0x18..], 0x80010000);
    BinaryPrimitives.WriteInt32LittleEndian(exe[0x1C..], exeLength - 0x800);
    BinaryPrimitives.WriteUInt16LittleEndian(exe[TableOffset..], 100);
    BinaryPrimitives.WriteUInt16LittleEndian(exe[(TableOffset + 2)..], 200);
    if (size == 2048) return logical;
    byte[] image = new byte[logical.Length / 2048 * size];
    for (int lba = 0; lba < logical.Length / 2048; lba++)
    {
        byte[] raw = new byte[2352];
        raw.AsSpan(1, 10).Fill(0xFF);
        int frame = lba + 150;
        raw[12] = Bcd(frame / 4500);
        raw[13] = Bcd(frame / 75 % 60);
        raw[14] = Bcd(frame % 75);
        raw[15] = 2;
        raw[18] = raw[22] = 8;
        logical.AsSpan(lba * 2048, 2048).CopyTo(raw.AsSpan(24));
        raw = RawMode2Form1SectorIntegrity.RebuildRawSectorCopy(raw, lba);
        raw.AsSpan(size == 2352 ? 0 : 16, size).CopyTo(image.AsSpan(lba * size));
    }
    return image;

    static byte Bcd(int value) => (byte)((value / 10 << 4) | value % 10);
}

static void Require(bool condition, string message)
{
    if (!condition) throw new InvalidOperationException(message);
}
