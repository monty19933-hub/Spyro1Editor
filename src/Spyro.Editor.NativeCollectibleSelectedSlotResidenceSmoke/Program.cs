using System.Security.Cryptography;
using Spyro.Editor.Core.Analysis;
using Spyro.Editor.Core.Persistence;

int checks = 0;
void Check(bool value, string why) { checks++; if (!value) throw new Exception(why); }
void Refuse(Action action, string why)
{
    try { action(); }
    catch (InvalidDataException) { checks++; return; }
    catch (ArgumentException) { checks++; return; }
    throw new Exception("Expected refusal: " + why);
}
static Guid Id(int n) => Guid.Parse($"10000000-0000-0000-0000-{n:x12}");
static string Hash(byte[] b) => Convert.ToHexStringLower(SHA256.HashData(b));
var content = Guid.Parse("12345678-1234-5678-9012-123456789abc");
DetachedCollectibleManifest Manifest(int n) => DetachedCollectibleManifest.CreateNew(content, Enumerable.Range(1, n).Select(Id));
var manifest = Manifest(2);
var state = DetachedCollectibleSave.CreateNew(manifest).WithCollected(0, Id(1)).WithCollected(1, Id(2));
byte[] originalManifest = manifest.SerializeCanonical(), originalSave = DetachedCollectibleSaveCodec.Serialize(state);
var neutral = NativeCollectibleSelectedSlotResidence.Describe(manifest, 0);
Check(!neutral.SourceEvidenceBound, "neutral sizing is not source capture");
Check(neutral.Evidence.Count == 0 && neutral.ContentId == content && neutral.ManifestSha256 == manifest.Sha256 &&
    neutral.ManifestRevision == manifest.Revision && neutral.OrdinarySlot == 0 && neutral.ActiveCount == 2,
    "neutral descriptor binds authored identity without claiming source evidence");
Check(DetachedCollectibleManifest.MaximumLifetimeInstances == 4096 && DetachedCollectibleSaveCodec.MaximumEncodedBytes == 0x680,
    "detached schema limits unchanged");
Check(neutral.Sizing.RawBitmapBytes == 1 && neutral.Sizing.AlignedBitmapBytes == 0x10 && neutral.Sizing.LiveFootprintBytes == 0xB0,
    "actual two-ID live state, not maximum envelope");
Check(neutral.Sizing.DetachedEnvelopeBytes == originalSave.Length && originalSave.Length == 0x83 && neutral.Sizing.NativeTransferBytes == 0x100,
    "existing serializer determines exact variable envelope formula");
Check(neutral.ProposedBase == 0x801FF600 && neutral.ProposedEnd == 0x801FF800 && neutral.ProposedNormalFloor == 0x801879B0,
    "guarded A00 reservation geometry");
Check(neutral.GuardOffset == 0 && neutral.GuardBytes == 0x10 && neutral.SelectedBitsOffset == 0x90 && neutral.ActiveMaskOffset == 0xA0,
    "sentinel cannot alias selected bits or metadata");
Check(NativeCollectibleSelectedSlotResidence.ReservationBytes == 0x200 &&
    NativeCollectibleSelectedSlotResidence.MetadataOffset == 0x10 && NativeCollectibleSelectedSlotResidence.MetadataBytes == 0x80 &&
    NativeCollectibleSelectedSlotResidence.BitmapOffset == 0x90 && NativeCollectibleSelectedSlotResidence.AbiMagic == 0x524C4353 &&
    NativeCollectibleSelectedSlotResidence.AbiVersion == 1 && NativeCollectibleSelectedSlotResidence.ReadyPhase == 1,
    "fixed proposal ABI is shared with detached kernel");
Check(new[] { NativeCollectibleSelectedSlotResidence.MagicOffset, NativeCollectibleSelectedSlotResidence.VersionOffset,
    NativeCollectibleSelectedSlotResidence.PhaseOffset, NativeCollectibleSelectedSlotResidence.OrdinarySlotOffset,
    NativeCollectibleSelectedSlotResidence.ControllerOffset, NativeCollectibleSelectedSlotResidence.DirtyOffset,
    NativeCollectibleSelectedSlotResidence.NativeFileOffset, NativeCollectibleSelectedSlotResidence.StatusOffset,
    NativeCollectibleSelectedSlotResidence.ContentIdOffset, NativeCollectibleSelectedSlotResidence.ManifestDigestOffset,
    NativeCollectibleSelectedSlotResidence.EnvelopeDigestOffset, NativeCollectibleSelectedSlotResidence.RevisionOffset,
    NativeCollectibleSelectedSlotResidence.LifetimeCountOffset, NativeCollectibleSelectedSlotResidence.BitmapBytesOffset,
    NativeCollectibleSelectedSlotResidence.ActiveCountOffset, NativeCollectibleSelectedSlotResidence.EarnedActiveCountOffset,
    NativeCollectibleSelectedSlotResidence.MutationGenerationOffset, NativeCollectibleSelectedSlotResidence.TransactionGenerationOffset,
    NativeCollectibleSelectedSlotResidence.OperationOffset, NativeCollectibleSelectedSlotResidence.FailureOffset,
    NativeCollectibleSelectedSlotResidence.SessionEpochOffset }.SequenceEqual(
        new[] { 0x10, 0x14, 0x16, 0x17, 0x18, 0x19, 0x1A, 0x1C, 0x20, 0x30, 0x50, 0x70, 0x74, 0x76, 0x78, 0x7A, 0x7C, 0x80, 0x84, 0x88, 0x8C }),
    "metadata field offsets cannot drift independently from smoke ABI");
Check(neutral.CopyActiveMask()[0] == 3 && neutral.CopyActiveMask().AsSpan(1).IndexOfAnyExcept((byte)0) < 0,
    "exact active mask and alignment zeros");
for (int slot = 0; slot < 3; slot++)
{
    var p = NativeCollectibleSelectedSlotResidence.Describe(manifest, slot);
    byte[] bits = p.CopySelectedSlotBits(state);
    Check(bits.Length == 0x10 && bits[0] == (slot == 0 ? 1 : slot == 1 ? 2 : 0) && bits.AsSpan(1).IndexOfAnyExcept((byte)0) < 0,
        "selected slot only and zero alignment " + slot);
    bits[0] ^= 0xFF; Check(p.CopySelectedSlotBits(state)[0] != bits[0], "detached selected copy " + slot);
}
foreach (int count in new[] { 1, 2, 7, 8, 9, 127, 128, 129, 1408, 1409, 4096 })
{
    var m = Manifest(count); var size = NativeCollectibleSelectedSlotResidence.MeasureLifetime(count);
    Check(size.RawBitmapBytes == (count + 7) / 8 && size.AlignedBitmapBytes % 16 == 0, "bit/byte boundary " + count);
    Check(size.DetachedEnvelopeBytes == DetachedCollectibleSaveCodec.Serialize(DetachedCollectibleSave.CreateNew(m)).Length,
        "no independent save serializer " + count);
    if (count <= 1408)
    {
        var p = NativeCollectibleSelectedSlotResidence.Describe(m, 2);
        Check(p.Sizing.FitsProposedReservation && p.Sizing.LiveFootprintBytes <= 0x200, "bounded admission " + count);
        byte[] active = p.CopyActiveMask();
        Check(Enumerable.Range(0, active.Length * 8).All(bit => ((active[bit / 8] >> (bit % 8)) & 1) == (bit < count ? 1 : 0)),
            "every active and unassigned mask bit " + count);
    }
    else
    {
        Check(!size.FitsProposedReservation, "oversize remains measurable " + count);
        Refuse(() => NativeCollectibleSelectedSlotResidence.Describe(m, 0), "oversize never truncated " + count);
    }
}
Check(NativeCollectibleSelectedSlotResidence.MaximumAdmittedLifetimeInstances == 1408, "physical profile limit, not schema cap");
var zeroSizing = NativeCollectibleSelectedSlotResidence.MeasureLifetime(0);
Check(zeroSizing.RawBitmapBytes == 0 && zeroSizing.AlignedBitmapBytes == 0 && zeroSizing.LiveFootprintBytes == 0x90 &&
    zeroSizing.DetachedEnvelopeBytes == 0x80 && zeroSizing.NativeTransferBytes == 0x80,
    "empty lifetime remains measurable without issuing a residence");
foreach (int bad in new[] { -1, 4097, int.MaxValue }) Refuse(() => NativeCollectibleSelectedSlotResidence.MeasureLifetime(bad), "bad lifetime " + bad);
foreach (int slot in new[] { -1, 3, int.MaxValue }) Refuse(() => NativeCollectibleSelectedSlotResidence.Describe(manifest, slot), "bad ordinary slot " + slot);
Refuse(() => NativeCollectibleSelectedSlotResidence.Describe(Manifest(0), 0), "zero custom content needs no new native reservation");
var large = Manifest(1409).Advance([], Enumerable.Range(1, 1408).Select(Id));
Check(large.ActiveCount == 1 && large.LifetimeCount == 1409, "retired IDs retain ownership");
Refuse(() => NativeCollectibleSelectedSlotResidence.Describe(large, 0), "retirement cannot fake smaller RAM requirement");
var retired = manifest.Advance([], [Id(1)]);
var retiredState = state.MigrateTo(retired);
var retiredPlan = NativeCollectibleSelectedSlotResidence.Describe(retired, 0);
Check(retiredPlan.CopyActiveMask()[0] == 2 && retiredPlan.CopySelectedSlotBits(retiredState)[0] == 1,
    "earned retired history retained separately from active mask");
Refuse(() => neutral.CopySelectedSlotBits(retiredState), "successor requires explicit new binding");
Refuse(() => retiredPlan.CopySelectedSlotBits(state), "predecessor cannot masquerade as successor");
var allRetired = manifest.Advance([], [Id(1), Id(2)]);
var allRetiredPlan = NativeCollectibleSelectedSlotResidence.Describe(allRetired, 0);
Check(allRetiredPlan.ActiveCount == 0 && allRetiredPlan.Sizing.LifetimeCount == 2 &&
    allRetiredPlan.CopyActiveMask().All(b => b == 0) && allRetiredPlan.CopySelectedSlotBits(state.MigrateTo(allRetired))[0] == 1,
    "fully retired manifest retains lifetime history and reserve geometry");
neutral.RequireManifest(DetachedCollectibleManifest.Parse(originalManifest)); checks++;
neutral.RequireManifest(DetachedCollectibleManifest.CreateNew(content, [Id(2), Id(1)])); checks++;
Refuse(() => neutral.RequireManifest(DetachedCollectibleManifest.CreateNew(Guid.NewGuid(), [Id(1), Id(2)])), "content identity mismatch");
Refuse(() => neutral.RequireManifest(DetachedCollectibleManifest.CreateNew(content, [Id(1), Id(3)])), "same count different IDs");
Refuse(() => neutral.RequireManifest(retired), "same bit width different revision/mask");
var mask = neutral.CopyActiveMask(); mask[0] = 0;
Check(neutral.CopyActiveMask()[0] == 3, "immutable mask exposure");
Check(manifest.SerializeCanonical().SequenceEqual(originalManifest) && DetachedCollectibleSaveCodec.Serialize(state).SequenceEqual(originalSave),
    "every detached original byte unchanged");
void Gates(NativeCollectibleSelectedSlotResidence p)
{
    Check(!p.NativeResidenceIssued && !p.Installed && !p.WholeEngineFitProven && !p.LifetimeProven && !p.CardIoApproved && !p.PublicationApproved,
        "all unissued residency/lifetime/publication gates remain false");
    Refuse(p.RequireNativeInstallation, "descriptor cannot install anything");
}
Gates(neutral);
Refuse(() => neutral.CopyOriginalExecutable(), "neutral descriptor has no original executable owner");
Refuse(() => neutral.CopyOriginalPeteHeader(), "neutral descriptor has no original PETE owner");
Refuse(() => neutral.RequireExactOriginal([], []), "neutral descriptor cannot validate fabricated source owners");
Refuse(() => NativeCollectibleSelectedSlotResidence.Describe(null!, 0), "missing manifest");
Refuse(() => neutral.RequireManifest(null!), "missing manifest binding");
Refuse(() => neutral.CopySelectedSlotBits(null!), "missing selected save");
if (args.Length == 0)
{
    Console.WriteLine($"COLLECTIBLE_SELECTED_SLOT_RESIDENCE_SOURCE_FREE_OK checks={checks} sourceReads=0 nativeResidenceIssued=false");
    return;
}
if (args.Length != 2 || args[0] != "--source") throw new ArgumentException("Usage: no args or --source <original-USA-BIN>");
using var usa = NativeRegionalSourceSession.Open(args[1], NativeDiscRevision.UsaScus94228);
byte[] exe = usa.ReadExecutableRange(0, usa.ExecutableByteLength), pete = usa.ReadWadRange(usa.Entries[8].Offset, 0x800);
byte[] keptExe = exe.ToArray(), keptPete = pete.ToArray();
var source = NativeCollectibleSelectedSlotResidence.Capture(usa, manifest, 0);
Check(source.SourceEvidenceBound && source.Evidence.Count >= 9, "actual exact-source evidence only");
Gates(source);
Check(source.OriginalReserveValue == 0x800 && source.ProposedReserveValue == 0xA00 && source.ReserveConstantOffset == 0x65DA4,
    "exact native constant beforeimage and uninstalled proposal");
Check(source.CopyOriginalExecutable().SequenceEqual(exe) && source.CopyOriginalPeteHeader().SequenceEqual(pete), "complete original owners retained");
source.RequireExactOriginal(exe, pete); checks++;
foreach (int at in new[] { 0, 0x10, 0x800, 0x3154, 0x4C014, 0x65DA3, 0x65DA4, 0x65DA8, 0x65FFF })
{
    byte[] bad = exe.ToArray(); bad[at] ^= 1;
    Refuse(() => source.RequireExactOriginal(bad, pete), "every tested EXE owner/complement mutation " + at);
    Refuse(() => NativeCollectibleSelectedSlotResidence.ValidateSourceBeforeimages(bad, pete), "whole EXE source hash " + at);
}
foreach (int at in new[] { 0, 4, 0x100, 0x7FF })
{
    byte[] bad = pete.ToArray(); bad[at] ^= 1;
    Refuse(() => source.RequireExactOriginal(exe, bad), "PETE complete complement " + at);
    Refuse(() => NativeCollectibleSelectedSlotResidence.ValidateSourceBeforeimages(exe, bad), "PETE hash " + at);
}
Refuse(() => source.RequireExactOriginal(exe[..^1], pete), "truncated executable");
Refuse(() => source.RequireExactOriginal([.. exe, 0], pete), "extra executable byte");
Refuse(() => source.RequireExactOriginal(exe, pete[..^1]), "truncated PETE header");
Refuse(() => source.RequireExactOriginal(exe, [.. pete, 0]), "extra PETE header byte");
Refuse(() => NativeCollectibleSelectedSlotResidence.ValidateSourceBeforeimages(exe[..^1], pete), "static truncated executable");
Refuse(() => NativeCollectibleSelectedSlotResidence.ValidateSourceBeforeimages([.. exe, 0], pete), "static extended executable");
Refuse(() => NativeCollectibleSelectedSlotResidence.ValidateSourceBeforeimages(exe, pete[..^1]), "static truncated PETE header");
Refuse(() => NativeCollectibleSelectedSlotResidence.ValidateSourceBeforeimages(exe, [.. pete, 0]), "static extended PETE header");
byte[] leaked = source.CopyOriginalExecutable(); leaked[0] ^= 1;
Check(source.CopyOriginalExecutable().SequenceEqual(exe), "source executable returned by copy");
leaked = source.CopyOriginalPeteHeader(); leaked[0] ^= 1;
Check(source.CopyOriginalPeteHeader().SequenceEqual(pete), "source PETE header returned by copy");
Check(exe.SequenceEqual(keptExe) && pete.SequenceEqual(keptPete) && usa.ReadExecutableRange(0, usa.ExecutableByteLength).SequenceEqual(keptExe) &&
    usa.ReadWadRange(usa.Entries[8].Offset, 0x800).SequenceEqual(keptPete), "every original source byte unchanged by descriptor");
usa.VerifyUnchanged();
Console.WriteLine($"COLLECTIBLE_SELECTED_SLOT_RESIDENCE_SOURCE_OK checks={checks} executableSha256={Hash(exe)} nativeResidenceIssued=false imageWrites=0 cardWrites=0");
