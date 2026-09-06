using System.Buffers.Binary;
using Spyro.Editor.Core.Exporting;

int checks = 0;
int refusals = 0;
void Check(bool value, string label)
{ if (!value) throw new InvalidOperationException(label); checks++; }
void Reject(Action action, string label)
{ try { action(); } catch (InvalidDataException) { checks++; refusals++; return; } throw new InvalidOperationException("Expected refusal: " + label); }
byte[] original = new byte[0x2800];
for (int i = 0; i < 4; i++) { Put(original, i * 8, (uint)(0x800 + i * 0x800)); Put(original, i * 8 + 4, 0x800); }
Put(original, 0x50, 0x1800); Put(original, 0x54, 0x1810); original[0x152] = 1;
for (int i = 0x800; i < original.Length; i++) original[i] = (byte)(i * 13 + 7);
var domain = new NativeAddressSpace(NativeAddressKind.LevelData, "composition-fixture", NativeAddressRevision.Original, original.Length);
byte[] Read(NativeByteExtent extent)
{
    if (extent.Space != domain) throw new InvalidDataException("Detached source address domain.");
    return original.AsSpan((int)extent.Offset, (int)extent.ByteLength).ToArray();
}
var source = NativeMobyModelRegistrationSource.DecodeFixture(domain, original);
NativeSkyRelocationPayload[] payloads =
[
    new(0, 37, 0x1000, 0, 0x800, 0, new byte[0x20], 0, false),
    new(1, 37, 0x1040, 0, 0x40, 0, new byte[0x14], 1, false),
    new(2, 37, 0x180C, 0, 0xC, 0, new byte[0x100], 2, false),
    new(3, 37, 0x2000, 0, 0x800, 0, new byte[0x300], 2, false)
];
var layout = new NativeLevelResourceCompositionLayout(source, 0, 0, payloads, [], Read);
byte[] final = layout.CopyFinalBytes();
Check(final.Length == original.Length + 0x1800, "all three owners rounded once; player and package growth share model padding");
Check(Word(final, 0x14) == 0x1000, "combined model exact400 bytes rounds to800 only once");
Check(Word(final, 0x50) == 0x2800 && Word(final, 0x54) == 0x2910, "root0 start and old generic root use distinct insertion relationships");
Check(new NativeLevelResourceCompositionLayout(source, 0, 0, payloads.Reverse().ToArray(), [], Read).CopyFinalBytes().AsSpan().SequenceEqual(final), "stage enumeration does not choose final bytes");
NativeResourceCompositionCases.Run(source, original, payloads, Read, Check, Reject);
Console.WriteLine($"NATIVE_RESOURCE_COMPOSITION_SOURCE_FREE_OK checks={checks} refused={refusals} publication=false runtime=false");
if (args.Length == 2 && args[0] == "--source")
{
    await NativeResourceCompositionSourceCases.RunAsync(args[1], Check, Reject);
    Console.WriteLine($"NATIVE_RESOURCE_COMPOSITION_SOURCE_OK checks={checks} refused={refusals} publication=false runtime=false");
}
else if (args.Length != 0) throw new InvalidDataException("Usage: no arguments, or --source <unchanged original BIN>.");
static void Put(byte[] bytes, int offset, uint value) => BinaryPrimitives.WriteUInt32LittleEndian(bytes.AsSpan(offset), value);
static uint Word(byte[] bytes, int offset) => BinaryPrimitives.ReadUInt32LittleEndian(bytes.AsSpan(offset));
