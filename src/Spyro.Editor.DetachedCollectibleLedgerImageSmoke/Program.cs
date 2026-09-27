using System.Buffers.Binary;
using System.Security.Cryptography;
using Spyro.Editor.Core.Analysis;
using Spyro.Editor.Core.Exporting;
using Spyro.Editor.Core.Persistence;

// No source/card/file reads. Independent literal ABI expectations below describe
// only a detached host image; CPU.Copy is an isolated research fixture operation.
int checks = 0;
void Check(bool value, string message) { if (!value) throw new Exception(message); checks++; }
void Refuse(Action action, string message)
{
    try { action(); }
    catch (Exception error) when (error is InvalidDataException or ArgumentException) { checks++; return; }
    throw new Exception("Expected refusal: " + message);
}
static Guid Id(int value) => Guid.Parse(value.ToString("x32"));
static DetachedCollectibleManifest Manifest(int count) => DetachedCollectibleManifest.CreateNew(
    Guid.Parse("89abcdef-0123-4567-89ab-cdef01234567"), Enumerable.Range(1, count).Select(Id));
static uint U32(byte[] bytes, int offset) => BinaryPrimitives.ReadUInt32LittleEndian(bytes.AsSpan(offset));
static ushort U16(byte[] bytes, int offset) => BinaryPrimitives.ReadUInt16LittleEndian(bytes.AsSpan(offset));
static string Hash(byte[] bytes) => Convert.ToHexStringLower(SHA256.HashData(bytes));
DetachedCollectibleLedgerImage Hydrate(DetachedCollectibleManifest manifest, DetachedCollectibleSave save,
    int slot = 0, int controller = 0, uint marker = 0xABCD, uint epoch = 0) =>
    DetachedCollectibleLedgerImage.Hydrate(NativeCollectibleSelectedSlotResidence.Describe(manifest, slot),
        manifest, DetachedCollectibleSaveCodec.Serialize(save), controller, marker, epoch);

void AssertImage(DetachedCollectibleLedgerImage image, DetachedCollectibleManifest manifest,
    DetachedCollectibleSave save, int slot, int controller, uint marker, uint epoch)
{
    byte[] bytes = image.CopyBytes(), envelope = DetachedCollectibleSaveCodec.Serialize(save);
    int raw = (manifest.LifetimeCount + 7) / 8, aligned = (raw + 15) & ~15, end = 0x90 + 2 * aligned;
    Check(bytes.Length == 0x200 && image.ByteLength == 0x200 && image.LiveFootprintBytes == end, "Exact full reservation and live extent.");
    Check(U32(bytes, 0x10) == 0x524C4353 && U16(bytes, 0x14) == 1 && bytes[0x16] == 1, "Detached ABI magic/version/phase.");
    Check(bytes[0x17] == slot && bytes[0x18] == controller && U16(bytes, 0x1A) == marker, "Exact selected tuple fields without narrowing.");
    Check(image.OrdinarySlot == slot && image.ControllerIndex == controller && image.NativeFileMarker == marker && image.SessionEpoch == epoch,
        "Reported scalar metadata is exact, not native binding authority.");
    Check(bytes.AsSpan(0x20, 16).SequenceEqual(manifest.SerializeCanonical().AsSpan(0x10, 16)), "Canonical big-endian GUID bytes.");
    Check(bytes.AsSpan(0x30, 32).SequenceEqual(Convert.FromHexString(manifest.Sha256)) && image.ManifestSha256 == manifest.Sha256,
        "Exact canonical manifest digest.");
    Check(bytes.AsSpan(0x50, 32).SequenceEqual(SHA256.HashData(envelope)) && image.EnvelopeSha256 == Hash(envelope),
        "Envelope hash derived from every validated envelope byte, including all three slots.");
    Check(U32(bytes, 0x70) == manifest.Revision && U16(bytes, 0x74) == manifest.LifetimeCount && U16(bytes, 0x76) == raw,
        "Revision, lifetime count and raw bitmap length retain full widths.");
    Check(U16(bytes, 0x78) == manifest.ActiveCount && U16(bytes, 0x7A) == save.EarnedActiveInstances(slot).Count,
        "Active and earned-active counts are recomputed.");
    Check(image.ActiveCount == manifest.ActiveCount && image.EarnedActiveCount == save.EarnedActiveInstances(slot).Count,
        "Reported counts agree with independent save query.");
    Check(bytes.AsSpan(0x90, raw).SequenceEqual(save.CopySlotBits(slot)), "Complete selected lifetime bitmap, not active-only state.");
    byte[] mask = new byte[aligned];
    foreach (var entry in manifest.Entries.Where(entry => !entry.Retired)) mask[entry.BitIndex / 8] |= (byte)(1 << (entry.BitIndex % 8));
    Check(bytes.AsSpan(0x90 + aligned, aligned).SequenceEqual(mask), "Exact active mask including zero padding.");
    Check(!bytes.AsSpan(0, 0x10).ContainsAnyExcept((byte)0) && !bytes.AsSpan(0x90 + raw, aligned - raw).ContainsAnyExcept((byte)0) &&
          !bytes.AsSpan(end).ContainsAnyExcept((byte)0), "Guard, bitmap alignment and unused reservation tail are zero.");
    Check(bytes[0x19] == 0 && U32(bytes, 0x1C) == 0 && U32(bytes, 0x7C) == 0 && U32(bytes, 0x80) == 0 &&
          U32(bytes, 0x84) == 0 && U32(bytes, 0x88) == 0 && U32(bytes, 0x8C) == epoch, "Clean idle counters/control words and exact opaque session.");
    Check(image.CopyValidatedEnvelope().SequenceEqual(envelope) && image.ImageSha256 == Hash(bytes), "Defensive complete envelope and image identities.");
    Check(image.DetachedEnvelopeValidated && !image.NativeBindingValidated && !image.ReadyAdmissionAuthorized && !image.NativeResidenceIssued &&
          !image.Installed && !image.LifetimeProven && !image.CardIoApproved && !image.PersistentSaveChanges && !image.PublicationApproved,
        "Host-compatible bytes never issue native binding, Ready, lifetime, card or installation authority.");
}

var manifest = Manifest(9);
var state = DetachedCollectibleSave.CreateNew(manifest).WithCollected(0, Id(1)).WithCollected(1, Id(2)).WithCollected(2, Id(9));
byte[] originalState = DetachedCollectibleSaveCodec.Serialize(state), originalManifest = manifest.SerializeCanonical();
foreach (int slot in Enumerable.Range(0, 3))
foreach (int controller in new[] { 0, 1 })
foreach (uint marker in new uint[] { 0, 0xFFFF })
foreach (uint epoch in new uint[] { 0, 1, uint.MaxValue })
    AssertImage(Hydrate(manifest, state, slot, controller, marker, epoch), manifest, state, slot, controller, marker, epoch);
Check(originalState.SequenceEqual(DetachedCollectibleSaveCodec.Serialize(state)) && originalManifest.SequenceEqual(manifest.SerializeCanonical()),
    "Hydration cannot mutate its save or manifest.");

byte[] input = originalState.ToArray();
var residence = NativeCollectibleSelectedSlotResidence.Describe(manifest, 1);
var isolated = DetachedCollectibleLedgerImage.Hydrate(residence, manifest, input, 1, 0x1234, uint.MaxValue);
byte[] first = isolated.CopyBytes(), envelopeCopy = isolated.CopyValidatedEnvelope();
input[0] ^= 0xFF; first[0x90] ^= 0xFF; envelopeCopy[0] ^= 0xFF;
Check(isolated.CopyValidatedEnvelope().SequenceEqual(originalState) && isolated.CopyBytes()[0x90] == 2, "Input/output arrays cannot mutate captured state.");
Refuse(() => isolated.RequireNativeInstallation(), "Installation always denied.");
var otherSlotChanged = state.WithCollected(2, Id(3));
var otherSlotImage = Hydrate(manifest, otherSlotChanged, 1, 1, 0x1234, uint.MaxValue);
Check(!otherSlotImage.EnvelopeSha256.Equals(isolated.EnvelopeSha256, StringComparison.Ordinal) &&
    otherSlotImage.CopyBytes().AsSpan(0x90, 16).SequenceEqual(isolated.CopyBytes().AsSpan(0x90, 16)),
    "Digest includes unselected slots; selected bitmap remains isolated.");

var nextManifest = manifest.Advance([Id(10)], [Id(1), Id(9)]);
var migrated = state.MigrateTo(nextManifest);
foreach (int slot in Enumerable.Range(0, 3)) AssertImage(Hydrate(nextManifest, migrated, slot), nextManifest, migrated, slot, 0, 0xABCD, 0);
Check(Hydrate(nextManifest, migrated).CopyBytes()[0x90] == 1 && U16(Hydrate(nextManifest, migrated).CopyBytes(), 0x7A) == 0,
    "Retired earned bit survives while retired count is excluded.");
Refuse(() => Hydrate(nextManifest, state), "No implicit predecessor migration.");
var allRetired = manifest.Advance([], manifest.Entries.Select(entry => entry.InstanceId));
var retiredState = state.MigrateTo(allRetired);
AssertImage(Hydrate(allRetired, retiredState), allRetired, retiredState, 0, 0, 0xABCD, 0);

foreach (int count in new[] { 1, 7, 8, 9, 127, 128, 129, 255, 256, 257, 1408 })
{
    var current = Manifest(count);
    var earned = DetachedCollectibleSave.CreateNew(current).WithCollected(2, current.Entries[^1].InstanceId);
    AssertImage(Hydrate(current, earned, 2), current, earned, 2, 0, 0xABCD, 0);
}
var denseManifest = Manifest(1408);
var dense = DetachedCollectibleSave.FromValidatedBits(denseManifest, Enumerable.Repeat((byte)0xFF, 3 * 176).ToArray());
AssertImage(Hydrate(denseManifest, dense, 2), denseManifest, dense, 2, 0, 0xABCD, 0);
Check(U16(Hydrate(denseManifest, dense, 2).CopyBytes(), 0x7A) == 1408, "Dense earned-active count is not narrowed to a byte.");
foreach (int count in new[] { 0, 1409, 4096 })
{
    var current = Manifest(count);
    Refuse(() => Hydrate(current, DetachedCollectibleSave.CreateNew(current)), "Unadmitted residence capacity.");
}
foreach (int controller in new[] { -1, 2, 16, int.MaxValue }) Refuse(() => Hydrate(manifest, state, controller: controller), "Controller index is0/1, not native times16.");
foreach (uint marker in new uint[] { 0x10000, uint.MaxValue }) Refuse(() => Hydrate(manifest, state, marker: marker), "Native marker cannot silently narrow.");
foreach (int slot in new[] { -1, 3 }) Refuse(() => Hydrate(manifest, state, slot), "Ordinary slot bounds.");
var wrongManifest = DetachedCollectibleManifest.CreateNew(Id(888), manifest.Entries.Select(entry => entry.InstanceId));
Refuse(() => DetachedCollectibleLedgerImage.Hydrate(residence, wrongManifest, originalState, 0, 0, 0), "Different content manifest.");
var remapped = DetachedCollectibleManifest.CreateNew(manifest.ContentId, Enumerable.Range(100, 9).Select(Id));
Refuse(() => DetachedCollectibleLedgerImage.Hydrate(residence, remapped, originalState, 0, 0, 0), "Same content/revision/count cannot hide different stable identities.");
Refuse(() => DetachedCollectibleLedgerImage.Hydrate(residence, nextManifest, originalState, 0, 0, 0), "Different manifest revision.");
var parsedManifest = DetachedCollectibleManifest.Parse(manifest.SerializeCanonical());
Check(DetachedCollectibleLedgerImage.Hydrate(residence, parsedManifest, originalState, 1, 0x1234, uint.MaxValue).CopyBytes()
    .SequenceEqual(isolated.CopyBytes()), "Exact canonical manifest binding is independent of object reference identity.");
Refuse(() => DetachedCollectibleLedgerImage.Hydrate(null!, manifest, originalState, 0, 0, 0), "Missing residence.");
Refuse(() => DetachedCollectibleLedgerImage.Hydrate(residence, null!, originalState, 0, 0, 0), "Missing manifest.");

List<byte[]> refused = [[], new byte[0x200], Enumerable.Repeat((byte)0xFF, 0x200).ToArray(), originalState[..^1], [.. originalState, 0]];
byte[] corrupt = originalState.ToArray(); corrupt[^1] ^= 1; refused.Add(corrupt);
byte[] unsupported = originalState.ToArray(); unsupported[8] = 2; refused.Add(unsupported);
byte[] padding = originalState.ToArray(); padding[DetachedCollectibleSaveCodec.HeaderBytes + 1] |= 0x80;
DetachedCollectibleBinary.WriteChecksum(padding); refused.Add(padding);
byte[] reserved = originalState.ToArray(); reserved[0x2C] = 1;
DetachedCollectibleBinary.WriteChecksum(reserved); refused.Add(reserved);
refused.Add(DetachedCollectibleSaveCodec.Serialize(DetachedCollectibleSave.CreateNew(wrongManifest)));
foreach (byte[] bytes in refused)
{
    byte[] before = bytes.ToArray();
    Refuse(() => DetachedCollectibleLedgerImage.Hydrate(residence, manifest, bytes, 0, 0, 0), "Unknown/corrupt/legacy/mismatched envelope has no replacement.");
    Check(bytes.SequenceEqual(before), "Rejected opaque bytes preserved exactly.");
}

// Actual unchanged collector execution proves hydration composition, not native
// residence. Stable bit255 is already earned; retired254 stays earned; collect256.
const uint Code = 0x800E0000, Ledger = 0x801F0000, Stop = 0x80010000;
var large = Manifest(257);
var collected = DetachedCollectibleSave.CreateNew(large).WithCollected(1, Id(255)).WithCollected(1, Id(256));
var largeNext = large.Advance([], [Id(255)]);
collected = collected.MigrateTo(largeNext);
var hydrated = Hydrate(largeNext, collected, 1, 1, 0xFFFF, uint.MaxValue);
byte[] ledger = hydrated.CopyBytes();
var space = new NativeAddressSpace(NativeAddressKind.LevelData, "detached-hydration-code-only", NativeAddressRevision.Final, 0x2000);
var capacity = new NativeByteExtent(space, 0, 0x2000);
var kernel = NativeCollectibleCollectionKernel.Compile(largeNext, 1, capacity, Code, Ledger);
RenderProbeCpu Run(byte[] bytes, uint bit, NativeCollectibleCollectionKernel selectedKernel, NativeCollectibleCollectionResult expected)
{
    var cpu = new RenderProbeCpu { StrictSignedArithmetic = true };
    cpu.Copy(Code, selectedKernel.CopyBytes()); cpu.Copy(Ledger, bytes);
    cpu.Regs[4] = Ledger; cpu.Regs[5] = bit; cpu.Regs[29] = 0x801D0000; cpu.Regs[31] = Stop;
    cpu.Run(Code, Stop);
    Check(cpu.Regs[2] == (uint)expected, "Actual collector result on hydrated bytes.");
    Check(cpu.Calls.Count == 0, "Collector fixture makes no native calls.");
    if (expected != NativeCollectibleCollectionResult.Collected) Check(cpu.Writes.Count == 0, "Duplicate/retired/wrong-slot refusal writes nothing.");
    return cpu;
}
Run(ledger, 254, kernel, NativeCollectibleCollectionResult.Retired);
Run(ledger, 255, kernel, NativeCollectibleCollectionResult.AlreadyEarned);
Run(ledger, 256, NativeCollectibleCollectionKernel.Compile(largeNext, 0, capacity, Code, Ledger), NativeCollectibleCollectionResult.InvalidLedger);
Run(Hydrate(largeNext, collected, 1, 1, 0xFFFF, 0).CopyBytes(), 256, kernel, NativeCollectibleCollectionResult.Collected);
var success = Run(ledger, 256, kernel, NativeCollectibleCollectionResult.Collected);
HashSet<uint> writes = [Ledger + 0x19, Ledger + 0x7A, Ledger + 0x7B, Ledger + 0x7C, Ledger + 0x7D, Ledger + 0x7E, Ledger + 0x7F, Ledger + 0xB0];
Check(success.Writes.Count == 8 && success.Writes.ToHashSet().SetEquals(writes), "Exactly the eight existing collector write bytes.");
Check(Enumerable.Range(0, ledger.Length).Where(offset => !writes.Contains(Ledger + (uint)offset)).All(offset => success.Get(Ledger + (uint)offset, 1) == ledger[offset]),
    "Guard, metadata, retired/earned bits, active mask and padding remain unchanged outside the write set.");
Check(success.Get(Ledger + 0xAF, 1) == 0xC0 && success.Get(Ledger + 0xB0, 1) == 1 && success.Get(Ledger + 0x7A, 2) == 2 &&
      success.Get(Ledger + 0x7C) == 1 && success.Get(Ledger + 0x19, 1) == 1, "255/256 are not aliased; retirement and count/generation semantics compose.");
byte[] afterCollection = Enumerable.Range(0, 0x200).Select(offset => (byte)success.Get(Ledger + (uint)offset, 1)).ToArray();
Run(afterCollection, 256, kernel, NativeCollectibleCollectionResult.AlreadyEarned);
Check(hydrated.CopyBytes().SequenceEqual(ledger) && hydrated.CopyValidatedEnvelope().SequenceEqual(DetachedCollectibleSaveCodec.Serialize(collected)),
    "CPU fixture mutations cannot mutate detached inputs or the retained envelope.");
Console.WriteLine($"DETACHED_COLLECTIBLE_LEDGER_IMAGE_OK checks={checks} sourceReads=0 imageWrites=0 cardWrites=0 nativeReady=false lifetime=false");
