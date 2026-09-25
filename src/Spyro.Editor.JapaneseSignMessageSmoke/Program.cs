using System.Buffers.Binary;
using Spyro.Editor.Core.Analysis;

if (args.Length == 4 && args[0] == "--write-japanese-sign-reference")
{
    ResearchPublication.Write(args[1], args[2], args[3]);
    return;
}

int checks = 0;
void Check(bool value, string message) { if (!value) throw new Exception(message); checks++; }
void Refuse(Action action)
{
    try { action(); } catch (InvalidDataException) { checks++; return; }
    throw new Exception("Expected bounded-message refusal.");
}
void Word(byte[] bytes, int offset, int value) => BinaryPrimitives.WriteInt32LittleEndian(bytes.AsSpan(offset), value);
int Read(byte[] bytes, int offset) => BinaryPrimitives.ReadInt32LittleEndian(bytes.AsSpan(offset));

if (args.Length == 3 && args[0] == "--appearance-source")
{
    var revision = args[1] switch { "japan" => NativeDiscRevision.JapanScps10083, "usa" => NativeDiscRevision.UsaScus94228,
        _ => throw new ArgumentException("Explicit japan or usa revision required.") };
    using var source = NativeRegionalSourceSession.Open(args[2], revision);
    AppearanceCases.VerifySource(source, Check);
    Console.WriteLine($"JAPANESE_OBJECT_APPEARANCE_SOURCE_OK checks={checks} revision={revision} candidateWrites=0");
    return;
}

if (args.Length == 3 && args[0] == "--font-sources")
{
    using var japan = NativeRegionalSourceSession.Open(args[1], NativeDiscRevision.JapanScps10083);
    using var usa = NativeRegionalSourceSession.Open(args[2], NativeDiscRevision.UsaScus94228);
    SignFontPortabilityCases.VerifySource(japan, usa, Check);
    SignFontVramCases.VerifySource(japan, usa, Check);
    Console.WriteLine($"JAPANESE_SIGN_FONT_CPU_PLAN_SOURCE_OK checks={checks} candidateWrites=0 runtimeApproved=false");
    return;
}

if (args.Length == 2 && args[0] == "--source")
{
    using var source = NativeRegionalSourceSession.Open(args[1], NativeDiscRevision.JapanScps10083);
    AppearanceCases.VerifySource(source, Check);
    EggModelCases.VerifySource(source, Check);
    SignDialogTerminationCases.VerifySource(source, Check);
    var signs = NativeJapaneseSignSource.CaptureAll(source);
    Check(signs.Count == 330, "All original signs identified.");
    Check(signs.Select(s => (s.DataWadEntry, s.TrueIndex)).Distinct().Count() == 330, "Source row identity remains unique.");
    Check(signs.Count(s => s.AnimationVariant == 0) == 223 && signs.Count(s => s.AnimationVariant == 1) == 48 &&
        signs.Count(s => s.AnimationVariant == 2) == 59, "Native three-variant census.");
    Check(signs.Max(s => s.Message.ByteLength) == 153, "Observed message policy origin.");
    Check(signs.Max(s => s.Message.LineSegments) == 9, "Observed line policy origin.");
    foreach (var sign in signs)
    {
        UnicodeCases.VerifySource(sign, Check);
        byte[] originalData = source.ReadWadEntry(sign.DataWadEntry);
        byte[] rebuilt = sign.Message.BuildPrivateData(sign.PropertiesSceneOffset);
        Check(originalData.AsSpan(sign.SceneDataOffset + sign.PropertiesSceneOffset, rebuilt.Length).SequenceEqual(rebuilt),
            $"Exact native private block readback entry{sign.DataWadEntry} T{sign.TrueIndex}.");
        Check(!sign.NativeImportApproved && !sign.Message.NativeImportApproved, "No source-only import promotion.");
    }
    EditPlanCases.VerifySource(source, signs, Check);
    source.VerifyUnchanged();
    Console.WriteLine($"JAPANESE_SIGN_MESSAGE_DISC_OK checks={checks} signs=330 originalFinal=true candidateWrites=0");
    return;
}
if (args.Length != 0) throw new ArgumentException("Use no arguments or --source <original-japan.bin>.");

byte[] firstBytes = [0x80, 0x20, 0x44, 0x81, 0xFF, 0x82];
var first = NativeJapaneseSignMessage.FromNativeBytes(firstBytes);
var second = NativeJapaneseSignMessage.FromNativeBytes([0x83, 0x84]);
firstBytes[0] = 0x85;
Check(first.CopyNativeBytes()[0] == 0x80 && second.CopyNativeBytes()[0] == 0x83, "Independent immutable messages.");
Check(first.Tokens[0].Kind == NativeJapaneseSignTokenKind.Glyph && first.Tokens[1].Kind == NativeJapaneseSignTokenKind.Space &&
    first.Tokens[2].Kind == NativeJapaneseSignTokenKind.TextColor && first.Tokens[4].Kind == NativeJapaneseSignTokenKind.LineBreak,
    "Native glyph/space/color/line controls distinguished.");
Check(first.LineSegments == 2 && first.ByteLength == 6, "Message shape.");
byte[] detached = first.CopyNativeBytes(); detached[0] = 0;
Check(first.CopyNativeBytes()[0] == 0x80, "Detached native bytes cannot mutate typed message.");
var completeFirst = first.AsCompleteDialog();
var completeSecond = second.AsCompleteDialog();
byte[] block1 = completeFirst.BuildPrivateData(0x200), block2 = completeSecond.BuildPrivateData(0x300);
Check(Read(block1, 12) == 0x218 && Read(block1, 16) == 0x214 && Read(block2, 12) == 0x318 && Read(block2, 16) == 0x314,
    "Each relocated sign owns both pointers independently.");
Check(block1.AsSpan(24, 7).SequenceEqual(completeFirst.CopyNativeBytes()) && block1[30] == 0xFF && block1[31] == 0,
    "Native line flush and NUL terminator retained.");
Check(Read(block1, 0) == 0 && Read(block1, 4) == 0 && Read(block1, 8) == 0 && Read(block1, 20) == 0,
    "Mutable runtime counters are fresh.");
Check(NativeJapaneseSignMessage.FromNativeBytes(Enumerable.Repeat((byte)0x80, 47).ToArray()).ByteLength == 47,
    "Largest native-buffer-safe line includes a separate NUL slot.");
foreach (byte value in new byte[] { 0, 0x19, 0x1F, 0x2A, 0x3A, 0x3E, 0x47, 0x7F, 0xFD, 0xFE })
    Refuse(() => NativeJapaneseSignMessage.FromNativeBytes([value]));
Refuse(() => NativeJapaneseSignMessage.FromNativeBytes([]));
Refuse(() => NativeJapaneseSignMessage.FromNativeBytes(Enumerable.Repeat((byte)0x80, 48).ToArray()));
Refuse(() => NativeJapaneseSignMessage.FromNativeBytes(Enumerable.Repeat((byte)0xFF, 9).ToArray()));
Refuse(() => NativeJapaneseSignMessage.FromNativeBytes(Enumerable.Repeat((byte)0x80, 154).ToArray()));
Refuse(() => completeFirst.BuildPrivateData(-4)); Refuse(() => completeFirst.BuildPrivateData(3)); Refuse(() => completeFirst.BuildPrivateData(int.MaxValue - 3));

// Two separately owned native sign messages; no source certificate is issued.
byte[] data = new byte[0x800]; Word(data, 0x18, 0x200); Word(data, 0x1C, 0x600);
int cursor = 0x288;
for (int i = 0; i < 7; i++) { Word(data, cursor, 8); cursor += 8; }
Word(data, cursor, 8 + 2 * 0x58); Word(data, cursor + 4, 2);
int row1 = cursor + 8, row2 = row1 + 0x58, properties = cursor + 8 + 2 * 0x58;
int p1 = properties + 4 - 0x200, p2 = p1 + block1.Length;
Word(data, properties, 4 + block1.Length + block2.Length);
Word(data, row1, p1); Word(data, row2, p2); data[row1 + 0x36] = data[row2 + 0x36] = 0x47;
data[row2 + 0x3C] = data[row2 + 0x3D] = 2;
completeFirst.BuildPrivateData(p1).CopyTo(data, p1 + 0x200); completeSecond.BuildPrivateData(p2).CopyTo(data, p2 + 0x200);
var sources = NativeJapaneseSignSource.DecodeLevel(data, 23, 0x800);
Check(sources.Count == 2 && sources[0].Message.NativeBytesHex != sources[1].Message.NativeBytesHex,
    "Two source-owned messages are independent.");
Check(sources[1].AnimationVariant == 2 && sources[0].PropertiesSceneOffset == p1, "Variant and properties source provenance.");
foreach (var change in new (int Offset, int Value)[]
{
    (0x18, int.MaxValue), (0x18, -4), (0x18, 0x201), (0x1C, 0),
    (0x288, 0), (0x288, 4), (0x288, int.MaxValue - 3),
    (cursor + 4, 4097), (cursor + 4, -1), (row1, -4), (row1, p2), (row1, p1 - 4),
    (0x200 + p1 + 12, p2 + 24), (0x200 + p1 + 16, p1 + 12), (0x200 + p1, 1)
})
{
    byte[] bad = data.ToArray(); Word(bad, change.Offset, change.Value);
    Refuse(() => NativeJapaneseSignSource.DecodeLevel(bad, 23, 0x800));
}
byte[] unterminated = data.ToArray(); unterminated.AsSpan(p1 + 0x200 + 24, block1.Length - 24).Fill(0x80);
Refuse(() => NativeJapaneseSignSource.DecodeLevel(unterminated, 23, 0x800));
byte[] unmatched = data.ToArray(); unmatched[row1 + 0x3C] = 1;
Refuse(() => NativeJapaneseSignSource.DecodeLevel(unmatched, 23, 0x800));
Refuse(() => NativeJapaneseSignSource.DecodeLevel(new byte[0x1C], 23, 0x800));
UnicodeCases.Run(Check);
SignDialogTerminationCases.Run(Check);
SignFontPortabilityCases.Run(Check);
SignFontVramCases.Run(Check);
AppearanceCases.Run(Check);
EggModelCases.Run(Check);
EditPlanCases.Run(Check);
ResearchPublication.RunSourceFree(Check);
Console.WriteLine($"JAPANESE_SIGN_MESSAGE_SOURCE_FREE_OK checks={checks} sourceReads=0 candidateWrites=0");
