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
byte[] component = Component(0, 0x20), bank = Bank();
var decoded = NativeSoundBankCodec.Decode(component, bank);
Check(decoded.Descriptors.Count == 2 && decoded.Map.Count == 256, "native fixed map and descriptor count");
Check(decoded.Descriptors[0].Sample.ByteLength == 0x20 && decoded.Descriptors[1].Sample.BankOffset == 0x20, "ADPCM sample extents");
Check(decoded.Descriptors.All(d => d.IsSupportedOneShot), "supported simple one-shots");
Check(decoded.CopyComponent().AsSpan().SequenceEqual(component) && decoded.CopyBank().AsSpan().SequenceEqual(bank), "exact opaque-byte roundtrip");
Check(!decoded.RuntimePlaybackProven, "no playback authority");
component[4] = 1; bank[0] = 0x30;
Check(decoded.Map[0] == 0 && decoded.CopyBank()[0] == 0, "caller buffers detached");
foreach ((int field, uint value) in new (int, uint)[] { (0, 0), (0x104, 0), (0x104, 257), (0x108, 1), (0x108, 0x80000), (0x108, uint.MaxValue) })
{
    byte[] bad = Component(0, 0x20); Put(bad, field, value);
    Reject(() => NativeSoundBankCodec.Decode(bad, Bank()), "malformed component/sample owner");
}
byte[] badMap = Component(0, 0x20); badMap[4 + 0x1F] = 2;
Reject(() => NativeSoundBankCodec.Decode(badMap, Bank()), "unowned map index");
byte[] badFrames = Bank(); badFrames[0x11] = 0; badFrames[0x31] = 0;
Reject(() => NativeSoundBankCodec.Decode(Component(0), badFrames), "unterminated ADPCM sample");
badFrames = Bank(); badFrames[0] = 0x50;
Reject(() => NativeSoundBankCodec.Decode(Component(0), badFrames), "unknown ADPCM predictor");
badFrames = Bank(); badFrames[0] = 0x0D;
Reject(() => NativeSoundBankCodec.Decode(Component(0), badFrames), "unknown ADPCM shift");
badFrames = Bank(); badFrames[1] = 8;
Reject(() => NativeSoundBankCodec.Decode(Component(0), badFrames), "unknown ADPCM flag");
Reject(() => NativeSoundBankCodec.Decode(Component(0), new byte[NativeSoundBankCodec.SpuUploadByteLength + 16]), "SPU upload domain overflow");
Reject(() => NativeSoundBankCodec.Decode([], Bank()), "truncated header");
Reject(() => NativeSoundBankCodec.Decode(Component(0), []), "empty bank");
Reject(() => NativeSoundBankCodec.Decode(Component(0), new byte[17]), "unaligned bank");
Reject(() => NativeSoundBankCodec.Decode(Component(0, 0x10), Bank()), "partially aliased sample owners");
var sharedSample = NativeSoundBankCodec.Decode(Component(0, 0), Bank());
Check(ReferenceEquals(sharedSample.Descriptors[0].Sample, sharedSample.Descriptors[1].Sample), "identical sample starts share bounded immutable ownership");
byte[] loopBank = Bank(); loopBank[1] = 6; loopBank[0x11] = 3;
var loop = NativeSoundBankCodec.Decode(Component(0), loopBank);
Check(!loop.Descriptors[0].IsSupportedOneShot && loop.CopyBank().AsSpan().SequenceEqual(loopBank), "loop can roundtrip but does not acquire append semantics");
byte[] flag4Bank = Bank(); flag4Bank[1] = 4;
Check(!NativeSoundBankCodec.Decode(Component(0), flag4Bank).Descriptors[0].IsSupportedOneShot, "loop-start flag is not silently treated as simple one-shot");
byte[] pitch = Component(0); BinaryPrimitives.WriteUInt16LittleEndian(pitch.AsSpan(0x112), 0x4000);
Check(!NativeSoundBankCodec.Decode(pitch, Bank()).Descriptors[0].IsSupportedOneShot, "unsupported pitch cannot import");

byte[] donorComponent = Component(0, 0x20), donorBank = Bank(); donorComponent[4 + 0x1F] = 1; donorComponent[4 + 0x20] = 1; donorBank[0x22] = 0x42;
var donor = NativeSoundBankCodec.Decode(donorComponent, donorBank);
byte[] level = Level(Component(0));
var explicitRequest = new NativeGlobalSoundRequest(0x1F, NativeGlobalSoundMappingPolicy.ExplicitRebindUnresolvedConsumers);
Reject(() => new NativeGlobalSoundMergeLayout(level, donor, [new(0x1F)]), "descriptor zero is owned, not absent");
var merge = new NativeGlobalSoundMergeLayout(level, donor, [explicitRequest]);
Check(merge.AppendedSampleBytes == 0x20 && merge.AppendedDescriptorCount == 1 && merge.FinalLength == level.Length + 0x1000, "two-subfile sector-rounded append");
Check(merge.FinalBankLength == 0x1000 && merge.Samples.Single().BankRelativeOffset == 0x800 && merge.Samples[0].SpuUploadAddress == 0x1810, "sample-bank, data and uploaded-SPU domains remain distinct");
Check(merge.Bindings.Single().FinalDescriptor == 1 && merge.Bindings[0].MappingChanged && !merge.Bindings[0].ConsumerOwnershipResolved, "explicit rebind remains unresolved");
var detachedPayloads = merge.CopyInsertionPayloads(); detachedPayloads[0].Bytes[0] ^= 0x10;
Check(merge.CopyInsertionPayloads()[0].Bytes[0] != detachedPayloads[0].Bytes[0], "internal insertion payload copies detached");
Check(merge.CopyNonHeaderMutations().All(f => f.DataOffset >= 0x800) && merge.CopyNonHeaderMutations().Count == 3, "composition exposes only original-address nonheader fields");
Check(merge.Preservations.Sum(p => p.ByteLength) + merge.FieldChanges.Where(f => f.OriginalDataOffset >= 0x800).Sum(f => f.ByteLength) == level.Length - 0x800, "every original nonheader byte has exactly one owner");
foreach (var extent in merge.Preservations)
    Check(level.AsSpan(extent.OriginalDataOffset, extent.ByteLength).SequenceEqual(merge.CopyFinalBytes().AsSpan(extent.FinalDataOffset, extent.ByteLength)), "protected complement exact");
byte[] finalCopy = merge.CopyFinalBytes(); finalCopy[^1] ^= 1;
Check(NativeSoundBankCodec.Hash(merge.CopyFinalBytes()) == merge.FinalSha256, "final payload detached");
var dedup = new NativeGlobalSoundMergeLayout(level, donor, [new(0x20, explicitRequest.MappingPolicy), explicitRequest]);
Check(dedup.AppendedDescriptorCount == 1 && dedup.AppendedSampleBytes == 0x20 && dedup.Bindings.All(b => b.FinalDescriptor == 1), "deterministic request sharing");
Check(new NativeGlobalSoundMergeLayout(level, donor, [explicitRequest, new(0x20, explicitRequest.MappingPolicy)]).FinalSha256 == dedup.FinalSha256, "request order-independent bytes");
var noOp = new NativeGlobalSoundMergeLayout(level, donor, [new(0)]);
Check(noOp.FinalSha256 == NativeSoundBankCodec.Hash(level) && noOp.AppendedDescriptorCount == 0 && noOp.AppendedSampleBytes == 0, "equivalent mapped resource is exact no-op");
byte[] pitchComponent = Component(0); pitchComponent[0x113] = 0x10;
var pitchDonor = NativeSoundBankCodec.Decode(pitchComponent, Bank());
var descriptorOnly = new NativeGlobalSoundMergeLayout(level, pitchDonor, [new(0, explicitRequest.MappingPolicy)]);
Check(descriptorOnly.AppendedSampleBytes == 0 && descriptorOnly.AppendedDescriptorCount == 1 && descriptorOnly.FinalLength == level.Length + 0x800, "equal sample different pitch reuses bank owner");
byte[] residentComponent = Component(0, 0x20); byte[] residentLevel = Level(residentComponent, 0x800, donorBank);
var residentRebind = new NativeGlobalSoundMergeLayout(residentLevel, donor, [explicitRequest]);
Check(residentRebind.AppendedSampleBytes == 0 && residentRebind.AppendedDescriptorCount == 0 && residentRebind.FinalLength == residentLevel.Length && residentRebind.Bindings[0].FinalDescriptor == 1, "existing unmapped descriptor reuse requires explicit map rebind only");
Reject(() => new NativeGlobalSoundMergeLayout(level, loop, [new(0, explicitRequest.MappingPolicy)]), "loop import has no implicit semantics");
Reject(() => new NativeGlobalSoundMergeLayout(level, donor, []), "empty request");
Reject(() => new NativeGlobalSoundMergeLayout(level, donor, [new(-1)]), "negative selector");
Reject(() => new NativeGlobalSoundMergeLayout(level, donor, [new(256)]), "oversized selector");
Reject(() => new NativeGlobalSoundMergeLayout(level, donor, [new(0), new(0)]), "duplicate selector");
Reject(() => new NativeGlobalSoundMergeLayout(level, donor, [null!]), "null request");
Reject(() => new NativeGlobalSoundMergeLayout(level, donor, [new(0, (NativeGlobalSoundMappingPolicy)42)]), "unknown mapping policy");
Reject(() => new NativeGlobalSoundMergeLayout(level, donor, [explicitRequest], dataWadOffset: long.MaxValue), "WAD owner overflow");
Reject(() => new NativeGlobalSoundMergeLayout(Level(Component(0), 0x7E800), donor, [explicitRequest]), "sector rounded cap refuses even when raw appended bank would fit");
Reject(() => new NativeGlobalSoundMergeLayout(Level(Component(Enumerable.Repeat(0, 256).ToArray())), donor, [explicitRequest]), "descriptor capacity cannot alias byte index");
byte[] invalidPortal = level.ToArray(); int dataOffset = (int)BinaryPrimitives.ReadUInt32LittleEndian(level.AsSpan(8)); Put(invalidPortal, dataOffset + 24, 257);
Reject(() => new NativeLevelSoundLayout(invalidPortal), "portal work budget");
Put(invalidPortal, dataOffset + 24, 1); Put(invalidPortal, dataOffset + 32, 0);
Reject(() => new NativeLevelSoundLayout(invalidPortal), "portal zero points");
byte[] hugeComponent = level.ToArray(); Put(hugeComponent, dataOffset, uint.MaxValue);
Reject(() => new NativeLevelSoundLayout(hugeComponent), "component overflow");
byte[] nonzeroHeader = level.ToArray(); nonzeroHeader[0x1D0] = 1;
Reject(() => new NativeLevelSoundLayout(nonzeroHeader), "unknown nested header is not relaxed");
byte[] animationLevel = level.ToArray(); int modelOffset = (int)BinaryPrimitives.ReadUInt32LittleEndian(animationLevel.AsSpan(0x10)); Put(animationLevel, modelOffset, uint.MaxValue);
var animationDonor = NativePlayerAnimationStreamCodec.Decode(AnimationStream(8), new byte[64]);
Reject(() => new NativePlayerAnimationMergeLayout(animationLevel, animationDonor, new byte[64], [], [8]), "empty root0 boundary remains unsupported and fails closed");
byte[] residentAnimation = AnimationStream(18); residentAnimation.CopyTo(animationLevel, modelOffset); Put(animationLevel, 0x54, (uint)(modelOffset + residentAnimation.Length));
var animationMerge = new NativePlayerAnimationMergeLayout(animationLevel, animationDonor, new byte[64], [], [8]);
var soundAlongside = new NativeGlobalSoundMergeLayout(animationLevel, donor, [explicitRequest]);
var combinedPayloads = soundAlongside.CopyInsertionPayloads().Concat([animationMerge.CopyInsertionPayload()! with { PatchIndex = 10 }]).ToArray();
byte[] combined = Compose(animationLevel, combinedPayloads, soundAlongside.CopyNonHeaderMutations());
Check(combined.Length == animationLevel.Length + 0x1800, "three-subfile simultaneous growth");
var combinedSound = new NativeLevelSoundLayout(combined);
Check(NativeGlobalSoundMergeLayout.Equivalent(combinedSound.Sound.Descriptors[combinedSound.Sound.Map[0x1F]], donor.Descriptors[1]), "combined relocated sound component/body fields");
int combinedStream = (int)BinaryPrimitives.ReadUInt32LittleEndian(combined.AsSpan(0x50)), combinedEnd = (int)BinaryPrimitives.ReadUInt32LittleEndian(combined.AsSpan(0x54));
Check(NativePlayerAnimationStreamCodec.Decode(combined.AsSpan(combinedStream, combinedEnd - combinedStream), new byte[64]).Records.Select(r => r.AnimationId).SequenceEqual(new[] { 18, 8 }), "combined animation8 prefix insertion preserves resident record");
Check(Compose(animationLevel, combinedPayloads.Reverse().ToArray(), soundAlongside.CopyNonHeaderMutations()).AsSpan().SequenceEqual(combined), "composition ordering deterministic");
var badMutation = soundAlongside.CopyNonHeaderMutations().Select(f => f with { Before = f.Before ^ 1 }).ToArray();
Reject(() => Compose(animationLevel, combinedPayloads, badMutation), "stale mutation preimage refuses");
string fakeSource = Path.Combine(Path.GetTempPath(), "native-sound-source-refusal-" + Guid.NewGuid().ToString("N") + ".bin");
try
{
    using (var created = new FileStream(fakeSource, FileMode.CreateNew, FileAccess.Write, FileShare.None)) created.WriteByte(0);
    await RejectAsync(() => NativeGlobalSoundSource.CaptureAsync(fakeSource), "actual public source factory rejects tiny fixture");
    Check(File.ReadAllBytes(fakeSource).AsSpan().SequenceEqual(new byte[] { 0 }), "source refusal preserves fixture");
    using (var sparse = new FileStream(fakeSource, FileMode.Open, FileAccess.Write, FileShare.None)) sparse.SetLength(NativeGlobalSoundSource.OriginalImageByteLength);
    await RejectAsync(() => NativeGlobalSoundSource.CaptureAsync(fakeSource), "exact-length sparse fixture is not original source identity");
    Check(new FileInfo(fakeSource).Length == NativeGlobalSoundSource.OriginalImageByteLength, "hash refusal preserves exact-length generated fixture");
}
finally { File.Delete(fakeSource); }
Console.WriteLine($"NATIVE_GLOBAL_SOUND_SOURCE_FREE_OK checks={checks} refused={refusals} runtime=false publication=false");

if (args.Length == 2 && args[0] == "--source")
{
    var source = await NativeGlobalSoundSource.CaptureAsync(args[1]);
    Check(source.Levels.Count == 35 && source.Levels.Sum(l => l.Sound.Descriptors.Count) == 2457, "all retail sound owners");
    foreach (var item in source.Levels)
    {
        var roundtrip = NativeSoundBankCodec.Decode(item.Sound.CopyComponent(), item.Sound.CopyBank());
        Check(roundtrip.ComponentSha256 == item.Sound.ComponentSha256 && roundtrip.BankSha256 == item.Sound.BankSha256, "all35 exact roundtrips");
        Check(item.BankWadOffset == item.DataWadOffset + item.BankDataOffset && item.SoundWadOffset == item.DataWadOffset + item.SoundDataOffset, "source WAD/data owner domains");
        foreach (var descriptor in roundtrip.Descriptors)
            Check(descriptor.Sample.BankOffset == descriptor.SampleOffset && descriptor.Sample.ByteLength > 0, "all2457 bounded native sample references");
    }
    var wizard = source.Levels.Single(l => l.LevelId == 33); var electric = wizard.Sound.Descriptors[wizard.Sound.Map[0x1F]];
    var playerSource = await NativePlayerAnimationSource.CaptureAsync(args[1]);
    Check(wizard.Sound.Map[0x1F] == 40 && electric.Sample.BankOffset == 0x346E0 && electric.Sample.ByteLength == 0x1D40 && electric.Pitch == 0x800 &&
        electric.Sample.Sha256 == "b03fdc973a8966a21326691f1546c19bf9e8d40d2eb15b00bec6cc9789599322", "native electric donor golden");
    foreach (int targetId in new[] { 12, 13, 14 })
    {
        var target = source.Levels.Single(l => l.LevelId == targetId);
        Check(target.Sound.Map[0x1F] == 0 && !target.Sound.Descriptors.Any(d => NativeGlobalSoundMergeLayout.Equivalent(d, electric)), "electric resource truly absent, mapping zero still owned");
        await RejectAsync(() => source.CompileMergeAsync(args[1], 33, targetId, [new(0x1F)]), "changed global mapping requires explicit policy");
        var plan = await source.CompileMergeAsync(args[1], 33, targetId, [explicitRequest]);
        string golden = targetId switch
        {
            12 => "c6b15c291f3e929d8f3a7f73dcbb535bf4c52f1352fa31282606b69ed526fd6f",
            13 => "331e93b543205b5063d17d32f6083e63942038a26c8a47dbc8f9c96c1c4838cb",
            14 => "6b58b78bd39fae6d6307760b94ee2ef90e94ef1d1d2117c81a403037c4e4c745",
            _ => throw new InvalidOperationException()
        };
        Check(plan.FinalDataSha256 == golden, "independent raw-sector splice oracle golden");
        Check(plan.ExactAppendedSampleBytes == 0x1D40 && plan.AppendedDescriptorCount == 1 && plan.SectorRoundedGrowth == 0x2800, "source electric two-subfile append");
        Check(plan.SpuUploadDomainFitProven && !plan.CompleteConsumerOwnershipProven && !plan.RuntimeFitProven && !plan.RuntimePlaybackProven && !plan.PublicationApproved, "source plan authority boundary");
        Check(plan.AppendedSamples.Single().BankRelativeOffset == target.Sound.BankByteLength && plan.FinalSpuBankByteLength == target.Sound.BankByteLength + 0x2000, "full old bank padding remains owned");
        byte[] bytes = plan.CopyFinalLevelData(); Check(NativeSoundBankCodec.Hash(bytes) == plan.FinalDataSha256, "whole final bytes bound");
        var result = new NativeLevelSoundLayout(bytes);
        Check(result.Sound.Descriptors.Take(target.Sound.Descriptors.Count).Select(d => d.Sha256).SequenceEqual(target.Sound.Descriptors.Select(d => d.Sha256)), "every old descriptor exact");
        Check(result.Sound.CopyBank().AsSpan(0, target.Sound.BankByteLength).SequenceEqual(target.Sound.CopyBank()), "entire bank including padding exact");
        var playerPlan = await playerSource.CompileMergeAsync(args[1], 33, targetId, [8]);
        using (var originalImage = File.OpenRead(args[1]))
        {
            var disc = DiscImage.DetectLayout(args[1]);
            byte[] original = DiscImage.ReadFileBytes(originalImage, disc, 37, target.DataWadOffset, target.DataByteLength);
            Check(NativeSoundBankCodec.Hash(original) == target.DataSha256, "composition exact original source");
            byte[] composed = Compose(original, plan.CopyInsertionPayloads().Concat([playerPlan.CopyInsertionPayload()! with { PatchIndex = 10 }]).ToArray(), plan.CopyNonHeaderMutations());
            Check(composed.Length == original.Length + 0x6800, "native animation plus sound grows three owners once");
            string composedGolden = targetId switch
            {
                12 => "3965dd80a3e3492348798554e1658921330f2e32ffdc76d455adc4425791a10d",
                13 => "26fd7d05216c2bfd727479f0aa5daadde854521a352e3bba37e6da82ee828481",
                14 => "20f91001daaa4f60dbaf12cbcd9fa4d950a5a3f8aba2da7157fa65300ef65313",
                _ => throw new InvalidOperationException()
            };
            Check(NativeSoundBankCodec.Hash(composed) == composedGolden, "independent animation plus sound whole-level oracle");
            var composedSound = new NativeLevelSoundLayout(composed);
            Check(NativeGlobalSoundMergeLayout.Equivalent(composedSound.Sound.Descriptors[composedSound.Sound.Map[0x1F]], electric), "native composed electric sample exact");
            byte[] geometry = DiscImage.ReadFileBytes(originalImage, disc, 37, 0x7B9AA8, 0x32374);
            int streamStart = (int)BinaryPrimitives.ReadUInt32LittleEndian(composed.AsSpan(0x50)), streamEnd = (int)BinaryPrimitives.ReadUInt32LittleEndian(composed.AsSpan(0x54));
            var composedPlayer = NativePlayerAnimationStreamCodec.Decode(composed.AsSpan(streamStart, streamEnd - streamStart), geometry);
            Check(composedPlayer.Records.Single(r => r.AnimationId == 8).Sha256 == "cf194a65fc9b3b8790f6cd96eae5ef309156d00ad205d85790b2128e57243eb0", "native composed animation8 exact");
            originalImage.Position = 0;
            Check(Convert.ToHexStringLower(await System.Security.Cryptography.SHA256.HashDataAsync(originalImage)) == NativeGlobalSoundSource.OriginalImageSha256, "composition leaves exact source unchanged");
            Console.WriteLine($"SOUND_PLAYER_COMPOSITION level={targetId} finalSha256={NativeSoundBankCodec.Hash(composed)}");
        }
        Console.WriteLine($"SOUND_TARGET level={targetId} finalSha256={plan.FinalDataSha256} planSha256={plan.PlanSha256} finalBank={plan.FinalSpuBankByteLength:X}");
    }
    var reused = await source.CompileMergeAsync(args[1], 33, 33, [new(0x1F)]);
    Check(reused.OriginalDataSha256 == reused.FinalDataSha256 && reused.SectorRoundedGrowth == 0, "native donor reuse exact");
    await RejectAsync(() => source.CompileMergeAsync(args[1], 33, 65, [new(0x1F)]), "ID65 has no independent source ownership");
    await RejectAsync(() => source.CompileMergeAsync(args[1], 99, 14, [new(0x1F)]), "unknown donor");
    await RejectAsync(() => NativeGlobalSoundSource.CaptureAsync(Path.Combine(AppContext.BaseDirectory, "Spyro.Editor.NativeGlobalSoundSmoke.dll")), "wrong source actual length");
    Console.WriteLine($"NATIVE_GLOBAL_SOUND_SOURCE_OK checks={checks} refused={refusals} levels=35 descriptors=2457 runtime=false publication=false");
}
else if (args.Length != 0) throw new InvalidDataException("Usage: no arguments, or --source <unchanged original BIN>.");

async Task RejectAsync(Func<Task> action, string label)
{
    try { await action(); }
    catch (InvalidDataException) { checks++; refusals++; return; }
    throw new InvalidOperationException("Expected refusal: " + label);
}

byte[] Compose(byte[] original, IReadOnlyList<NativeSkyRelocationPayload> payloads, IReadOnlyList<NativeSoundOriginalFieldMutation> mutations)
{
    // Test-only, original-based composition of insertion-only producers. This
    // does not expose a generic publication API or accept replaced source owners.
    if (payloads.Any(p => p.OriginalLength != 0)) throw new InvalidDataException("Composition fixture supports insertions only.");
    var growth = new NativeLevelDataGrowthLayout(original.AsSpan(0, 0x800), original.Length, payloads);
    byte[] final = growth.Expand(original);
    int MapField(int offset, int width)
    {
        var owner = growth.Subfiles.Single(s => offset >= s.OriginalOffset && offset + (long)width <= s.OriginalOffset + s.OriginalLength);
        int relative = offset - owner.OriginalOffset;
        if (owner.Payloads.Any(p => p.ModelBlockOffset > relative && p.ModelBlockOffset < relative + width))
            throw new InvalidDataException("Composition insertion splits a field or protected extent.");
        return owner.FinalOffset + relative + owner.Payloads.Where(p => p.ModelBlockOffset <= relative).Sum(p => p.Bytes.Length);
    }
    foreach (var field in mutations)
    {
        int mapped = MapField(field.DataOffset, field.ByteLength);
        uint before = field.ByteLength == 1 ? final[mapped] : BinaryPrimitives.ReadUInt32LittleEndian(final.AsSpan(mapped));
        if (before != field.Before) throw new InvalidDataException("Composition mutation preimage changed.");
        if (field.ByteLength == 1) final[mapped] = checked((byte)field.After); else Put(final, mapped, field.After);
    }
    foreach (var owner in growth.Subfiles)
    {
        int start = owner.OriginalOffset, end = start + owner.OriginalLength;
        var boundaries = new[] { start, end }.Concat(owner.Payloads.Select(p => start + p.ModelBlockOffset))
            .Concat(mutations.Where(f => f.DataOffset >= start && f.DataOffset < end).SelectMany(f => new[] { f.DataOffset, f.DataOffset + f.ByteLength })).Distinct().Order().ToArray();
        for (int i = 0; i + 1 < boundaries.Length; i++)
        {
            int a = boundaries[i], n = boundaries[i + 1] - a;
            if (n == 0 || mutations.Any(f => a >= f.DataOffset && a + n <= f.DataOffset + f.ByteLength)) continue;
            Check(original.AsSpan(a, n).SequenceEqual(final.AsSpan(MapField(a, n), n)), "composition protects every other original byte including models/scene/padding");
        }
    }
    return final;
}

static void Put(byte[] bytes, int offset, uint value) => BinaryPrimitives.WriteUInt32LittleEndian(bytes.AsSpan(offset), value);
static byte[] Component(params int[] offsets)
{
    byte[] bytes = new byte[0x108 + offsets.Length * 20]; Put(bytes, 0, (uint)bytes.Length); Put(bytes, 0x104, (uint)offsets.Length);
    for (int i = 0; i < offsets.Length; i++)
    {
        Put(bytes, 0x108 + i * 20, (uint)offsets[i]);
        BinaryPrimitives.WriteUInt16LittleEndian(bytes.AsSpan(0x108 + i * 20 + 8), 0x32);
        BinaryPrimitives.WriteUInt16LittleEndian(bytes.AsSpan(0x108 + i * 20 + 10), 0x800);
    }
    return bytes;
}
static byte[] Bank()
{
    byte[] bytes = new byte[0x80]; bytes[0x11] = 1; bytes[0x31] = 1; return bytes;
}
static byte[] Level(byte[] component, int bankLength = 0x800, byte[]? bankPrefix = null)
{
    int vramLength = 0x80000 + bankLength, data = 0x800 + vramLength;
    int dataLength = (32 + component.Length + 0x7FF) / 0x800 * 0x800;
    int model = data + dataLength, scene = model + 0x800;
    byte[] bytes = new byte[scene + 0x800];
    Put(bytes, 0, 0x800); Put(bytes, 4, (uint)vramLength); Put(bytes, 8, (uint)data); Put(bytes, 12, (uint)dataLength);
    Put(bytes, 16, (uint)model); Put(bytes, 20, 0x800); Put(bytes, 24, (uint)scene); Put(bytes, 28, 0x800);
    Put(bytes, 0x50, (uint)model); Put(bytes, 0x54, (uint)(model + 4)); bytes[0x152] = 0x26;
    (bankPrefix ?? Bank()).CopyTo(bytes, 0x80800); bytes[data - 1] = 0xEF;
    for (int i = 0; i < 6; i++) Put(bytes, data + i * 4, 4);
    Put(bytes, data + 24, 0); Put(bytes, data + 28, 4); component.CopyTo(bytes, data + 32);
    bytes[model - 1] = 0xDC; bytes[scene - 1] = 0xBA; bytes[^1] = 0x98;
    return bytes;
}
static byte[] AnimationStream(int id)
{
    byte[] bytes = new byte[0x8C]; Put(bytes, 0, (uint)id); Put(bytes, 4, 0x80);
    BinaryPrimitives.WriteInt16LittleEndian(bytes.AsSpan(8), 2); bytes[12] = 1;
    Put(bytes, 8 + 0x18, 16); Put(bytes, 8 + 0x24, 0xFF000000); Put(bytes, 8 + 0x28, 0xFF000008);
    Put(bytes, bytes.Length - 4, uint.MaxValue); return bytes;
}
