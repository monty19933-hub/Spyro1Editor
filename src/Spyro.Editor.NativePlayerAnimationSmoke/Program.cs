using System.Buffers.Binary;
using Spyro.Editor.Core.Analysis;
using Spyro.Editor.Core.Exporting;

int checks = 0, refusals = 0;
void Check(bool value, string label)
{
    if (!value) throw new InvalidOperationException(label);
    checks++;
}
void Reject(Action action, string label)
{
    try { action(); }
    catch (InvalidDataException) { checks++; refusals++; return; }
    throw new InvalidOperationException("Expected refusal: " + label);
}
byte[] geometry = new byte[64];
byte[] fixture = Stream(8, 33);
var decoded = NativePlayerAnimationStreamCodec.Decode(fixture, geometry);
Check(decoded.Encode().AsSpan().SequenceEqual(fixture), "exact stream round trip");
Check(decoded.Records.Select(r => r.AnimationId).SequenceEqual(new[] { 8, 33 }), "record order");
Check(decoded.Records.All(r => r.FrameCount == 2), "signed frame counts");
Check(decoded.Records[0].Frames[0].PackedSourceWord == 0xAA000000 && decoded.Records[0].Frames[0].UpperByte == 0xAA &&
    decoded.Records[0].Frames[0].LocalByteOffset == 0 && decoded.Records[0].Frames[0].LoaderPreservedBits == 0xAA000000,
    "packed frame offset, high bits and highest byte remain distinct exact source fields");
Check(decoded.Records[0].References.Count(r => r.Domain == NativePlayerAnimationReferenceDomain.LocalFrameData) == 3,
    "local data and packed frame references");
Check(decoded.Records[0].References.Count(r => r.Domain == NativePlayerAnimationReferenceDomain.SharedPlayerGeometry) == 2,
    "shared geometry references remain a separate domain");
Check(!decoded.FramePayloadDecodingProven && !decoded.RuntimeFitProven, "no transitive consumer or runtime authority");
fixture[8] = 99; geometry[0] = 99;
Check(decoded.Records[0].FrameCount == 2 && decoded.Encode()[8] == 2, "caller buffers detached");
byte[] copy = decoded.Records[0].CopyRecord(); copy[0] = 0;
Check(decoded.Records[0].AnimationId == 8 && decoded.Encode()[0] == 8, "returned record copy detached");
foreach (int id in new[] { int.MinValue, -2, 46, int.MaxValue })
    Reject(() => NativePlayerAnimationStreamCodec.Decode(Stream(id), new byte[64]), "invalid ID");
Reject(() => NativePlayerAnimationStreamCodec.Decode(Stream(8, 8), new byte[64]), "duplicate ID");
Reject(() => NativePlayerAnimationStreamCodec.Decode([], new byte[64]), "empty/truncated stream");
Reject(() => NativePlayerAnimationStreamCodec.Decode(new byte[4], new byte[64]), "missing record length");
Reject(() => NativePlayerAnimationStreamCodec.Decode(Stream(8)[..^4], new byte[64]), "missing terminator");
Reject(() => NativePlayerAnimationStreamCodec.Decode(Stream(8).Concat(new byte[4]).ToArray(), new byte[64]), "tail after terminator");
Reject(() => NativePlayerAnimationStreamCodec.Decode(Stream(8), []), "missing geometry owner");
foreach ((int field, uint value) in new (int, uint)[]
{
    (4, uint.MaxValue), (4, 0), (4, 0x21), (4, 0x1000), (8, 0), (8, 0xFFFF),
    (8 + 0x10, 0x100), (8 + 0x14, 64), (8 + 0x18, 0x80000000),
    (8 + 0x14, 1), (8 + 0x24, 0x1FFFFF), (8 + 0x24, 0x1FFFFE), (8 + 0x24, 1)
})
{
    byte[] bad = Stream(8); Put(bad, field, value);
    Reject(() => NativePlayerAnimationStreamCodec.Decode(bad, new byte[64]), "malformed length/frame/reference");
}
byte[] type = Stream(8); type[12] = 2;
Reject(() => NativePlayerAnimationStreamCodec.Decode(type, new byte[64]), "unsupported frame encoding");
Check(NativePlayerAnimationStreamCodec.Decode(Stream(), new byte[64]).Records.Count == 0, "canonical empty stream");

byte[] destination = Entry(Stream(18));
var donor = NativePlayerAnimationStreamCodec.Decode(Stream(8, 33), new byte[64]);
var merge = new NativePlayerAnimationMergeLayout(destination, donor, new byte[64], [0, 1], [33, 8]);
byte[] final = merge.CopyFinalBytes();
Check(merge.AddedIds.SequenceEqual(new[] { 8, 33 }) && merge.RequestedIds.SequenceEqual(new[] { 8, 33 }), "canonical requested order");
Check(merge.ExactGrowth == 0x110 && final.Length == destination.Length + 0x800, "exact insertion versus sector growth");
Check(NativeSkyWadRelocator.ExpandNestedLevelEntry(destination, [merge.CopyInsertionPayload()!], 0x800).AsSpan().SequenceEqual(final),
    "typed payload agrees with existing single-subfile relocation adapter");
Check(Word(final, 0x50) == Word(destination, 0x50) && Word(final, 0x54) == Word(destination, 0x54) + 0x110,
    "special root anchor and following package root relocation");
Check(Word(final, 0x14) == 0x1000 && Word(final, 0x18) == 0x2800, "model and scene descriptors grow/shift");
foreach (var preserved in merge.Preservations)
    Check(destination.AsSpan(preserved.OriginalOffset, preserved.ByteLength).SequenceEqual(final.AsSpan(preserved.FinalOffset, preserved.ByteLength)),
        "protected entire subfile/model interval " + preserved.Id);
Check(merge.Preservations.Sum(p => p.ByteLength) == destination.Length - 0x800, "every old non-header byte protected");
Check(final.AsSpan(0x800, 0x1000).SequenceEqual(destination.AsSpan(0x800, 0x1000)), "VRAM/SPU and level-data unaffected");
Check(final.AsSpan(0x2800, 0x800).SequenceEqual(destination.AsSpan(0x2000, 0x800)), "whole scene and all rows untouched");
Check(new NativePlayerAnimationMergeLayout(destination, donor, new byte[64], [0, 1], [8, 33]).FinalSha256 == merge.FinalSha256,
    "request permutation does not change bytes");
var reuse = new NativePlayerAnimationMergeLayout(final, donor, new byte[64], [0, 1], [8, 33, 0]);
Check(reuse.AddedIds.Count == 0 && reuse.ReusedLocalIds.SequenceEqual(new[] { 8, 33 }) && reuse.ReusedSharedIds.SequenceEqual(new[] { 0 }),
    "equivalent local reuse plus explicit shared reuse");
Check(reuse.CopyFinalBytes().AsSpan().SequenceEqual(final) && reuse.HeaderChanges.Count == 0, "reuse is exact byte identity");
var payloadCopy = merge.CopyInsertionPayload()!; payloadCopy.Bytes[0] = 0;
Check(merge.CopyInsertionPayload()!.Bytes[0] == 8, "payload accessor cannot mutate plan");
Check(payloadCopy.OriginalWadOffset == Word(destination, 0x54) - 4 && payloadCopy.SubfileIndex == 2 &&
    payloadCopy.OriginalLength == 0 && !payloadCopy.RequireLengthPrefix,
    "typed insertion uses exact source WAD address, not the whole-entry base");
var located = new NativePlayerAnimationMergeLayout(destination, donor, new byte[64], [], [8], 18, 0x148A000);
Check(located.CopyInsertionPayload()!.OriginalWadOffset == 0x148A000 + Word(destination, 0x54) - 4 &&
    located.CopyInsertionPayload()!.StorageWadEntry == 18, "typed payload retains actual source owner");
var otherGrowth = new NativeSkyRelocationPayload(1, 37, 0x1100, 0, 0x100, 0, new byte[16], 1, false);
var composedLayout = new NativeLevelDataGrowthLayout(destination.AsSpan(0, 0x800), destination.Length,
    [merge.CopyInsertionPayload()!, otherGrowth]);
byte[] composed = composedLayout.Expand(destination);
int composedStart = checked((int)Word(composed, 0x50)), composedEnd = checked((int)Word(composed, 0x54));
var composedStream = NativePlayerAnimationStreamCodec.Decode(composed.AsSpan(composedStart, composedEnd - composedStart), new byte[64]);
Check(composedStream.Records.Select(r => r.AnimationId).SequenceEqual(new[] { 18, 8, 33 }), "animation insertion composes with independent subfile growth");
Check(composedStart == 0x2000 && Word(composed, 0x18) == 0x3000 && composed.Length == destination.Length + 0x1000,
    "multi-subfile composition relocates player root and scene without fixed offsets");
Check(composed.AsSpan(0x3000, 0x800).SequenceEqual(destination.AsSpan(0x2000, 0x800)), "composed entire original scene preserved");
byte[] changedGeometry = new byte[64]; changedGeometry[0] = 1;
Reject(() => new NativePlayerAnimationMergeLayout(destination, donor, changedGeometry, [], [8]), "different shared geometry identity");
Reject(() => new NativePlayerAnimationMergeLayout(destination, donor, new byte[64], [8], [8]), "donor overrides shared slot");
Reject(() => new NativePlayerAnimationMergeLayout(destination, donor, new byte[64], [18], [8]), "destination overrides shared slot");
Reject(() => new NativePlayerAnimationMergeLayout(destination, donor, new byte[64], [], [9]), "requested resource missing in donor");
Reject(() => new NativePlayerAnimationMergeLayout(destination, donor, new byte[64], [], [8, 8]), "duplicate merge IDs");
Reject(() => new NativePlayerAnimationMergeLayout(destination, donor, new byte[64], [], []), "empty merge request");
Reject(() => new NativePlayerAnimationMergeLayout(destination, donor, new byte[64], [], [46]), "merge ID overflow");
Reject(() => new NativePlayerAnimationMergeLayout(destination, donor, new byte[64], [0, 0], [8]), "duplicate shared ownership");
byte[] conflicting = Stream(8); conflicting[0x68] = 1;
Reject(() => new NativePlayerAnimationMergeLayout(Entry(conflicting), donor, new byte[64], [], [8]), "different destination animation bytes");
foreach ((int field, uint value) in new (int, uint)[] { (0x54, 0), (0x50, 0x2000), (0x150, 1), (0x1D0, 1), (0x10, uint.MaxValue), (0x14, 0) })
{
    byte[] invalid = destination.ToArray(); Put(invalid, field, value);
    Reject(() => new NativePlayerAnimationMergeLayout(invalid, donor, new byte[64], [], [8]), "malformed destination owner");
}
Check(destination.AsSpan().SequenceEqual(Entry(Stream(18))), "refused and accepted plans preserve original input");
Reject(() => new NativePlayerAnimationMergeLayout(destination, donor, new byte[64], [], [8], 1, long.MaxValue), "overflowing WAD owner");
Reject(() => new NativePlayerAnimationMergeLayout(destination, donor, new byte[64], [], [8], 1, long.MaxValue - destination.Length),
    "expanded WAD owner overflow");
byte[] requestBufferGeometry = new byte[64];
var requestBuffer = new List<int> { 8 };
var detached = new NativePlayerAnimationMergeLayout(destination, donor, requestBufferGeometry, [], requestBuffer);
requestBuffer.Clear(); requestBufferGeometry[0] = 1;
byte[] finalCopy = detached.CopyFinalBytes(); finalCopy[0x1000] = 255;
Check(detached.RequestedIds.SequenceEqual(new[] { 8 }) && detached.CopyFinalBytes()[0x1000] == destination[0x1000],
    "requests, geometry and returned level bytes cannot mutate a completed layout");
Reject(() => NativePlayerAnimationStreamCodec.Decode(new byte[NativePlayerAnimationStreamCodec.MaximumStreamBytes + 4], new byte[64]),
    "stream allocation budget");
Reject(() => NativePlayerAnimationStreamCodec.Decode(Stream(), new byte[NativePlayerAnimationStreamCodec.MaximumSharedGeometryBytes + 4]),
    "shared geometry allocation budget");
string fakeSource = Path.Combine(Path.GetTempPath(), "spyro-player-animation-source-" + Guid.NewGuid().ToString("N") + ".bin");
try
{
    using (var created = new FileStream(fakeSource, FileMode.CreateNew, FileAccess.Write, FileShare.None)) created.WriteByte(0);
    Reject(() => NativePlayerAnimationSource.CaptureAsync(fakeSource).GetAwaiter().GetResult(), "real public source factory rejects tiny fake image");
    Check(File.ReadAllBytes(fakeSource).AsSpan().SequenceEqual(new byte[] { 0 }), "refused source file remains unchanged");
    // Sparse, generated fixture: exact source length is not source identity.
    // This never copies, changes or replaces the user's original BIN.
    using (var sparse = new FileStream(fakeSource, FileMode.Open, FileAccess.Write, FileShare.None))
        sparse.SetLength(NativePlayerAnimationSource.OriginalImageByteLength);
    Reject(() => NativePlayerAnimationSource.CaptureAsync(fakeSource).GetAwaiter().GetResult(), "exact-length fake source hash refusal");
    Check(new FileInfo(fakeSource).Length == NativePlayerAnimationSource.OriginalImageByteLength,
        "hash refusal leaves generated source fixture unchanged");
}
finally { File.Delete(fakeSource); }
Console.WriteLine($"NATIVE_PLAYER_ANIMATION_SOURCE_FREE_OK checks={checks} refused={refusals} runtime=false publication=false");

if (args.Length == 2 && args[0] == "--source")
{
    var source = await NativePlayerAnimationSource.CaptureAsync(args[1]);
    Check(source.Levels.Count == 35 && source.Levels.Sum(l => l.Stream.Records.Count) == 181, "all retail streams/records");
    Check(source.SharedAnimationIds.Count == 22 && !source.SharedAnimationIds.Contains(8), "actual shared player ownership");
    Check(source.Levels.Where(l => l.Stream.Records.Any(r => r.AnimationId == 8)).Select(l => l.LevelId)
        .SequenceEqual(new[] { 30, 33, 34, 40, 41, 44, 53 }), "all original electrocution residents");
    foreach (var level in source.Levels)
    {
        // Encode is byte-exact, not a reconstruction that silently repairs flags.
        byte[] encoded = level.Stream.Encode();
        Check(NativePlayerAnimationStreamCodec.Hash(encoded) == level.Stream.Sha256, "round trip level " + level.LevelId);
        foreach (var record in level.Stream.Records)
        {
            Check(record.StreamOffset >= 0 && record.StreamOffset + record.ByteLength <= level.Stream.TerminatorOffset,
                "record contained in exact retail stream");
            Check(record.References.Count == record.FrameCount + 3, "all native loader reference fields declared");
        }
    }
    var wizard = source.Levels.Single(l => l.LevelId == 33).Stream.Records.Single(r => r.AnimationId == 8);
    Check(wizard.ByteLength == 0x3E1C && wizard.FrameCount == 34 &&
        wizard.Sha256 == "cf194a65fc9b3b8790f6cd96eae5ef309156d00ad205d85790b2128e57243eb0", "native animation8 record golden");
    using (var originalImage = File.OpenRead(args[1]))
    {
        var disc = DiscImage.DetectLayout(args[1]);
        byte[] sharedGeometry = DiscImage.ReadFileBytes(originalImage, disc, 37, 0x7B9AA8, 0x32374);
        Check(NativePlayerAnimationStreamCodec.Hash(sharedGeometry) == source.SharedGeometrySha256, "independent shared geometry read");
        var wizardStream = source.Levels.Single(l => l.LevelId == 33).Stream;
        byte[] nativeExe=DiscImage.ReadFileBytes(originalImage,disc,53875,0,0x66000);
        var alignment=NativePlayerAnimationModelAlignment.Capture(nativeExe);
        for(int pc=0x3A30;pc<0x3BE0;pc+=4){var mutated=nativeExe.ToArray();mutated[pc]^=1;Reject(()=>NativePlayerAnimationModelAlignment.Capture(mutated),"complete native parser mutation");}
        byte[] canonical=source.Levels[0].Stream.Encode();var padded=canonical.Concat(new byte[4]).ToArray();var issued=alignment.DecodeModelOwner(padded,sharedGeometry);
        Check(issued.Padding?.ByteLength==4&&issued.Stream.Encode().SequenceEqual(canonical),"issued four zero bytes preserve complete stream");issued.Padding!.Require(padded);
        Reject(()=>NativePlayerAnimationStreamCodec.Decode(padded,sharedGeometry),"public codec remains strict about trailing bytes");
        foreach(var bad in new[]{canonical.Concat(new byte[8]).ToArray(),canonical.Concat(new byte[]{1,0,0,0}).ToArray(),canonical.Concat(new byte[2]).ToArray()}){Reject(()=>alignment.DecodeModelOwner(bad,sharedGeometry),"extra/nonzero/truncated gap");Reject(()=>issued.Padding.Require(bad),"issued padding extent/payload mutation");}
        // A source-shaped odd-sized record exercises both padding transitions.
        byte[] odd=Stream(8);Array.Resize(ref odd,odd.Length+4);Put(odd,4,0x84);Put(odd,odd.Length-4,uint.MaxValue);Put(odd,odd.Length-8,0);
        var oddDonor=NativePlayerAnimationStreamCodec.Decode(odd,new byte[64]);var oddMerge=new NativePlayerAnimationMergeLayout(Entry(Stream(18)),oddDonor,new byte[64],[],[8],modelAlignment:alignment);
        Check(oddMerge.ExactGrowth==0x90&&oddMerge.AlignmentPaddingOwner?.ByteLength==4,"four-byte gap preserves native rootsmod8");
        var onePad=oddMerge.CopyFinalBytes();var second=Stream(9);Array.Resize(ref second,second.Length+4);Put(second,4,0x84);Put(second,second.Length-4,uint.MaxValue);Put(second,second.Length-8,0);
        var consume=new NativePlayerAnimationMergeLayout(onePad,NativePlayerAnimationStreamCodec.Decode(second,new byte[64]),new byte[64],[],[9],modelAlignment:alignment);
        Check(consume.ExactGrowth==0x88&&consume.AlignmentPaddingOwner==null,"second odd insertion consumes only the owned four-byte gap");
        var reused=new NativePlayerAnimationMergeLayout(onePad,oddDonor,new byte[64],[],[8],modelAlignment:alignment);Check(reused.ExactGrowth==0&&reused.FinalSha256==oddMerge.FinalSha256,"padded repeated merge keeps exact no-op identity");
        for(int slot=1;slot<64;slot++)if(Word(onePad,0x50+slot*4)!=0)Check((Word(consume.CopyFinalBytes(),0x50+slot*4)-Word(onePad,0x50+slot*4))%8==0,"all later roots preserve modulo8 through gap consumption");
        foreach (var level in source.Levels)
        {
            byte[] original = DiscImage.ReadFileBytes(originalImage, disc, 37, level.DataWadOffset, level.DataByteLength);
            Check(NativePlayerAnimationStreamCodec.Hash(original) == level.DataSha256, "source matrix exact level hash");
            var matrix = new NativePlayerAnimationMergeLayout(original, wizardStream, sharedGeometry,
                source.SharedAnimationIds, [8], level.DataWadEntry, level.DataWadOffset,alignment);
            bool resident = level.Stream.Records.Any(r => r.AnimationId == 8);
            Check(matrix.ExactGrowth == (resident ? 0 : 0x3E20) && matrix.FinalByteLength - matrix.OriginalByteLength == (resident ? 0 : 0x4000),
                "all35 native targets: exact reuse or actual append");
            byte[] output = matrix.CopyFinalBytes();
            Check(original.AsSpan(checked((int)Word(original, 0x18)), checked((int)Word(original, 0x1C)))
                .SequenceEqual(output.AsSpan(checked((int)Word(output, 0x18)), checked((int)Word(output, 0x1C)))),
                "all35 native target scenes/rows/fixups preserved");
            Check(matrix.Preservations.Sum(p => p.ByteLength) == original.Length - 0x800, "all35 entire old non-header complements protected");
        }
        originalImage.Position = 0;
        Check(Convert.ToHexStringLower(await System.Security.Cryptography.SHA256.HashDataAsync(originalImage)) == NativePlayerAnimationSource.OriginalImageSha256 &&
            originalImage.Length == NativePlayerAnimationSource.OriginalImageByteLength && originalImage.Position == originalImage.Length,
            "original source identity preserved after all35 native merge matrix");
    }
    var toasty = await source.CompileMergeAsync(args[1], 33, 14, [8]);
    Check(toasty.AddedAnimationIds.SequenceEqual(new[] { 8 }) && toasty.ExactInsertedByteLength == 0x3E20 && toasty.SectorRoundedGrowth == 0x4000,
        "real Toasty missing-animation merge");
    Check(toasty.Preservations.Sum(p => p.ByteLength) == toasty.OriginalDataByteLength - 0x800, "all original Toasty payload bytes preserved");
    Check(!toasty.RuntimeFitProven && !toasty.CompleteAnimationConsumersProven && !toasty.PublicationApproved,
        "real structural merge still requires RAM and dependency acceptance");
    Check(toasty.FinalDataSha256=="4d0018961c179297b2ca41cd3cc55792db8ea5ea5c5f5a0f51ff486844c56e33","Toasty alignment-preserved source-byte golden");
    Console.WriteLine($"NATIVE_PLAYER_ANIMATION_MERGE target=14 ids=8 exact=0x{toasty.ExactInsertedByteLength:X} sector=0x{toasty.SectorRoundedGrowth:X} sha256={toasty.FinalDataSha256} plan={toasty.PlanSha256}");
    var flight = await source.CompileMergeAsync(args[1], 64, 14, [35, 33, 34]);
    Check(flight.AddedAnimationIds.SequenceEqual(new[] { 33, 34, 35 }) && flight.ExactInsertedByteLength == 0x33C8,
        "native free-flight records through same generic merge");
    Check(flight.FinalDataSha256 == "251770f0eade8981fd6fca94d3a0bf434baeaff23ee83520dda0bf9d78079c5b", "Toasty free-flight byte golden");
    string[] flightGoldens =
    [
        "4a45930544f1f603b90b66b7c3a719d423d229fc4143878f839b874a93eea526",
        "118ffb78244fdb8b121aca7d730f28d3f4f1b80211f1e1456092175ff9f71d54",
        "cfd726820dec1c745cf41c8f314c4f6462bac26102082a159bb75813ae331e7c"
    ];
    for (int i = 0; i < 3; i++)
    {
        Check(source.Levels.Single(l => l.LevelId == 64).Stream.Records.Single(r => r.AnimationId == 33 + i).Sha256 == flightGoldens[i],
            "native free-flight donor record golden");
        Check(source.Levels.Single(l => l.LevelId == 64).Stream.Records.Single(r => r.AnimationId == 33 + i).Frames
            .All(f => f.UpperByte == 0xFF), "all eleven native free-flight frame high bytes remain FF");
    }
    Check(wizard.Frames.All(f => f.UpperByte == 0xFF), "all34 native electric frame high bytes remain FF");
    foreach (int invalid in new[] { 65, -1, 16 })
        Reject(() => source.CompileMergeAsync(args[1], 33, invalid, [8]).GetAwaiter().GetResult(), "nonretail destination source authority");
    Reject(() => source.CompileMergeAsync(args[1], 65, 14, [8]).GetAwaiter().GetResult(), "nonretail donor authority");
    Console.WriteLine($"NATIVE_PLAYER_ANIMATION_MERGE target=14 ids=33,34,35 exact=0x{flight.ExactInsertedByteLength:X} sector=0x{flight.SectorRoundedGrowth:X} sha256={flight.FinalDataSha256} plan={flight.PlanSha256}");
    var same = await source.CompileMergeAsync(args[1], 33, 30, [8, 0]);
    Check(same.AddedAnimationIds.Count == 0 && same.ReusedLocalAnimationIds.SequenceEqual(new[] { 8 }) &&
        same.ReusedSharedAnimationIds.SequenceEqual(new[] { 0 }) && same.OriginalDataSha256 == same.FinalDataSha256,
        "real equivalent local/shared reuse is no-op");
    Console.WriteLine($"NATIVE_PLAYER_ANIMATION_SOURCE_OK checks={checks} refused={refusals} levels=35 records=181 runtime=false publication=false");
}
else if (args.Length != 0) throw new InvalidOperationException("Usage: [--source <original.bin>]");

static void Put(byte[] b, int o, uint v) => BinaryPrimitives.WriteUInt32LittleEndian(b.AsSpan(o), v);
static uint Word(byte[] b, int o) => BinaryPrimitives.ReadUInt32LittleEndian(b.AsSpan(o));
static byte[] Entry(byte[] stream)
{
    byte[] entry = new byte[0x2800];
    for (int i = 0; i < 4; i++) { Put(entry, i * 8, (uint)(0x800 + i * 0x800)); Put(entry, i * 8 + 4, 0x800); }
    for (int i = 0x800; i < entry.Length; i++) entry[i] = (byte)(i * 31 + 7);
    Put(entry, 0x50, 0x1800); Put(entry, 0x54, (uint)(0x1800 + stream.Length));
    BinaryPrimitives.WriteUInt16LittleEndian(entry.AsSpan(0x152), 0x26);
    stream.CopyTo(entry, 0x1800); return entry;
}
static byte[] Stream(params int[] ids)
{
    byte[] bytes = new byte[ids.Length * 0x88 + 4];
    for (int i = 0; i < ids.Length; i++)
    {
        int p = i * 0x88; Put(bytes, p, unchecked((uint)ids[i])); Put(bytes, p + 4, 0x80);
        BinaryPrimitives.WriteInt16LittleEndian(bytes.AsSpan(p + 8), 2);
        bytes[p + 12] = 1; Put(bytes, p + 8 + 0x18, 16);
        Put(bytes, p + 8 + 0x24, 0xAA000000); Put(bytes, p + 8 + 0x28, 8);
    }
    Put(bytes, bytes.Length - 4, uint.MaxValue); return bytes;
}
