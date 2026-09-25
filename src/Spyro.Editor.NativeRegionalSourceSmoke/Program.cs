using System.Buffers.Binary;
using System.Security.Cryptography;
using System.Text.Json;
using Spyro.Editor.Core.Analysis;

int checks = 0;
void Check(bool value, string message)
{
    if (!value) throw new InvalidOperationException(message);
    checks++;
}
void Refuse(Action action)
{
    try { action(); }
    catch (Exception e) when (e is InvalidDataException or ArgumentOutOfRangeException or ObjectDisposedException)
    { checks++; return; }
    throw new InvalidOperationException("Expected an exact-source/range refusal.");
}
string Hash(byte[] data) => Convert.ToHexStringLower(SHA256.HashData(data));
void Word(byte[] data, int offset, uint value) => BinaryPrimitives.WriteUInt32LittleEndian(data.AsSpan(offset), value);

if (args.Length == 3 && args[0] == "--source")
{
    NativeDiscRevision revision = args[1] switch
    {
        "japan" => NativeDiscRevision.JapanScps10083,
        "usa" => NativeDiscRevision.UsaScus94228,
        _ => throw new ArgumentException("Use japan or usa, never an inferred region.")
    };
    using var source = NativeRegionalSourceSession.Open(args[2], revision);
    bool japan = revision == NativeDiscRevision.JapanScps10083;
    Check(source.Entries.Count == 256 && !source.NativeImportApproved, "Complete directory, no import approval.");
    Check(source.Serial == (japan ? "SCPS_100.83" : "SCUS_942.28"), "Native revision retained.");
    Check(Hash(source.ReadExecutableRange(0, source.ExecutableByteLength)) == source.ExecutableSha256,
        "Complete executable source hash.");
    int sharedIndex = japan ? 21 : 8;
    byte[] shared = source.ReadWadEntry(sharedIndex);
    int runtimeCopyBytes = checked((int)BinaryPrimitives.ReadUInt32LittleEndian(shared));
    byte[] sharedHeader = shared[..2048];
    byte[] completeCopy = source.ReadWadRange(source.Entries[sharedIndex].Offset + 2048L, runtimeCopyBytes);
    if (japan)
    {
        Check(Hash(source.ReadWadRange(0, 2048)) == "ae42b988d2f57b99713e0d2b8a406415c445045864c69a28c4b2cca38fc0cbfc", "JP directory hash.");
        Check(Hash(source.ReadWadEntry(23)) == "7184fff950efa2bdffa37f97875ef7de2a3219480c61ec3ab37f784778d8cfde", "JP Artisans complete data owner.");
        Check(Hash(sharedHeader) == "8c507181496740d252c553544925205f75ce80dd78aa22793a823a414e67f0d6", "JP shared header owner.");
        Check(source.Entries[8] == new NativeRegionalWadEntry(8, 0x7BA800, 0), "Unused regional slot retains nonzero offset.");
        Refuse(() => source.ReadWadEntry(8));
        Check(runtimeCopyBytes == 0x2F734 && completeCopy.Length == runtimeCopyBytes, "JP runtime copy is explicit, not clipped to directory length.");
    }
    else
    {
        Check(Hash(sharedHeader) == "b9a4de193b5cf0bd9592de15c57aa5daa3713c3575026154b45402f3aceef6f1", "USA shared header unchanged.");
        Check(Hash(completeCopy) == "1703ac37e59e480909b6f66dc856d4e5f3ce80551f18cbc8c06129c3b54dd35d", "USA complete shared copy unchanged.");
        Check(runtimeCopyBytes == 0x39C48, "USA shared length is not reused for JP.");
    }
    Refuse(() => source.ReadExecutableRange(source.ExecutableByteLength - 1, 2));
    Refuse(() => source.ReadWadRange(source.WadByteLength - 1, 2));
    Refuse(() => source.ReadWadEntry(-1));
    Refuse(() => source.ReadWadEntry(256));
    Refuse(() => NativeRegionalSourceSession.Open(args[2], japan ? NativeDiscRevision.UsaScus94228 : NativeDiscRevision.JapanScps10083));
    source.VerifyUnchanged();
    Console.WriteLine(JsonSerializer.Serialize(new
    {
        source.Revision, source.Serial, source.SourceImageSha256, source.SourceImageByteLength,
        source.ExecutableSha256, source.ExecutableByteLength, source.WadByteLength,
        source.Entries, SharedIndex = sharedIndex, RuntimeCopyBytes = runtimeCopyBytes,
        RuntimeCopySha256 = Hash(completeCopy), HeldAndNamedOriginalFinal = true,
        NativeImportApproved = false, CandidateWritten = false, NativeRuntimeTested = false
    }, new JsonSerializerOptions { WriteIndented = true }));
    source.Dispose();
    Refuse(() => source.ReadWadEntry(sharedIndex));
    Refuse(source.VerifyUnchanged);
    Console.WriteLine($"NATIVE_REGIONAL_SOURCE_DISC_OK checks={checks} originalFinal=true revision={revision}");
    return;
}
if (args.Length != 0) throw new ArgumentException("Use no arguments or --source <japan|usa> <original.bin>.");

byte[] directory = new byte[2048];
Word(directory, 0, 2048); Word(directory, 4, 4096);
Word(directory, 8, 6144); // Empty Japanese-style reservation.
Word(directory, 16, 8192); Word(directory, 20, 2048);
var decoded = NativeRegionalSourceSession.DecodeDirectory(directory, 12288);
Check(decoded.Length == 256 && decoded[0] == new NativeRegionalWadEntry(0, 2048, 4096), "Directory ownership.");
Check(decoded[1] == new NativeRegionalWadEntry(1, 6144, 0), "Empty reservation retained.");
Word(directory, 0, 4096);
Check(decoded[0].Offset == 2048, "Captured directory independent from mutable input.");
Word(directory, 0, 2048);
foreach (var mutation in new (int Offset, uint Value)[]
{
    (0, 0), (0, 1), (0, uint.MaxValue), (4, 1), (4, uint.MaxValue),
    (16, 4096), (16, 2048), (16, 12288), (8, 14336), (8, 1)
})
{
    byte[] changed = directory.ToArray(); Word(changed, mutation.Offset, mutation.Value);
    Refuse(() => NativeRegionalSourceSession.DecodeDirectory(changed, 12288));
}
Refuse(() => NativeRegionalSourceSession.DecodeDirectory(directory.AsSpan(0, 2047), 12288));
Refuse(() => NativeRegionalSourceSession.DecodeDirectory(directory, 12287));
Refuse(() => NativeRegionalSourceSession.DecodeDirectory(directory, -1));
foreach (var range in new (long Offset, int Length, int Owner)[]
{
    (-1, 1, 2048), (0, 0, 2048), (0, -1, 2048), (2048, 1, 2048),
    (long.MaxValue, 1, 2048), (0, int.MaxValue, int.MaxValue), (2047, 2, 2048),
    (0, 1, -1), (0, 16 * 1024 * 1024 + 1, 32 * 1024 * 1024)
}) Refuse(() => NativeRegionalSourceSession.RequireRange(range.Offset, range.Length, range.Owner));
NativeRegionalSourceSession.RequireRange(0, 2048, 2048); checks++;
NativeRegionalSourceSession.RequireRange(2047, 1, 2048); checks++;
Refuse(() => NativeRegionalSourceSession.Open("unused-no-file-access", (NativeDiscRevision)42));
Console.WriteLine($"NATIVE_REGIONAL_SOURCE_FREE_OK checks={checks} sourceReads=0 candidateWrites=0");
