using System.Buffers.Binary;
using Spyro.Editor.Core.Exporting;

int checks = 0, refusals = 0;
void Check(bool condition, string label)
{
    if (!condition) throw new InvalidOperationException(label);
    checks++;
}
void Reject(Action action, string label, string? message = null)
{
    try { action(); }
    catch (InvalidDataException e)
    {
        Check(message == null || e.Message.Contains(message, StringComparison.Ordinal), label + ": diagnostic");
        refusals++; return;
    }
    throw new InvalidOperationException("Expected atomic refusal: " + label);
}
NativeLevelSlotRequest Request(string owner, int level, params int[] indexes) =>
    new(owner, level, NativeLevelSlotIntent.ReplaceExisting, indexes);
NativeByteExtent Resource(NativeLevelSlotBinding slot, NativeLevelSlotResourceKind kind) =>
    slot.Resources.Single(r => r.Kind == kind).Extent;

byte[] header = FixtureHeader();
NativeAddressSpace wad = new(NativeAddressKind.WadFile, "synthetic-not-source-authority", NativeAddressRevision.Original, 0x100000);
NativeAddressSpace exe = new(NativeAddressKind.ExecutableFile, "synthetic-not-source-authority", NativeAddressRevision.Original, 0x66000);
var layout = new NativeLevelSlotOwnershipLayout(header, wad, exe, []);
Check(layout.Slots.Count == 35 && layout.Slots.Select(s => s.LevelId).Distinct().Count() == 35, "35 native IDs");
Check(layout.Slots.Select(s => s.ContinuousIndex).SequenceEqual(Enumerable.Range(0, 35)), "continuous indexes");
Check(layout.Slots.All(s => s.OverlayDirectoryIndex == 9 + s.ContinuousIndex * 2 &&
    s.DataDirectoryIndex == 10 + s.ContinuousIndex * 2), "native paired directory geometry");
Check(layout.Slots.Count(s => s.SaveRows.Any(r => r.Kind == "eggs")) == 18, "egg save capacity");
Check(layout.RequireSlot(64).SaveRows.Single(r => r.Kind == "object-retirement").SaveOffset == 0x54C, "last retail bitmap");
Check(layout.RequireSlot(11).ContinuousIndex == 1 && layout.RequireSlot(20).ContinuousIndex == 6, "identity arithmetic");
foreach (int invalid in new[] { int.MinValue, -1, 0, 9, 16, 17, 19, 26, 59, 66, 100, int.MaxValue })
    Reject(() => layout.Reserve([Request("bad", invalid)]), "non-native ID alias");
Reject(() => layout.Reserve([Request("id65", 65)]), "reserved ID65", "no bounded late-music row35");
Reject(() => layout.Reserve([Request("new", 11) with { Intent = NativeLevelSlotIntent.AddIndependent }]),
    "existing slot is not a new independent level", "Independent native levels are unsupported");
Reject(() => layout.Reserve([Request("x", 11), Request("x", 13)]), "duplicate owner");
Reject(() => layout.Reserve([Request("x", 11), Request("y", 11)]), "duplicate native/save identity", "save index1");
Reject(() => layout.Reserve([Request("bad owner", 11)]), "unsafe identity grammar");
Reject(() => layout.Reserve([Request("x", 11) with { Intent = (NativeLevelSlotIntent)99 }]), "unknown intent");
Reject(() => layout.Reserve([]), "empty composition");
Reject(() => layout.Reserve([null!]), "null request");
Reject(() => layout.Reserve([Request("x", 11) with { RetirementTrueIndexes = null! }]), "missing persistent owners");
Reject(() => layout.Reserve([Request("x", 11, -1)]), "negative retirement index");
Reject(() => layout.Reserve([Request("x", 11, 256)]), "retirement bit overflow");
Reject(() => layout.Reserve([Request("x", 11, 2, 2)]), "same-level retirement alias");
Reject(() => layout.Reserve([Request("x", 40) with { RequiresEggPersistence = true }]), "missing egg row");
Check(layout.Reserve([Request("x", 35) with { RequiresEggPersistence = true }]).Reservations.Count == 1, "last egg row accepted");

int[] mutableIndexes = [255, 0];
var mutableRequests = new List<NativeLevelSlotRequest> { Request("stone", 11, mutableIndexes), Request("town", 13, 0, 255) };
NativeByteExtent stoneData = Resource(layout.RequireSlot(11), NativeLevelSlotResourceKind.LevelData);
NativeByteExtent townData = Resource(layout.RequireSlot(13), NativeLevelSlotResourceKind.LevelData);
var writes = new List<NativeLevelSlotDeclaredWrite>
{
    new("stone.scene", "stone", new(wad, stoneData.Offset + 64, 16)),
    new("town.scene", "town", new(wad, townData.Offset + 64, 16)),
    new("stone.music", "stone", Resource(layout.RequireSlot(11), NativeLevelSlotResourceKind.InitialMusicRow)),
    new("town.music", "town", Resource(layout.RequireSlot(13), NativeLevelSlotResourceKind.InitialMusicRow))
};
var accepted = layout.Reserve(mutableRequests, writes);
string golden = accepted.GeometrySha256;
Check(accepted.Reservations.Count == 2 && accepted.DeclaredWrites.Count == 4, "two-slot resource composition");
Check(accepted.Reservations.All(r => r.RetirementTrueIndexes.SequenceEqual(new[] { 0, 255 })), "same indexes in distinct level bitmaps accepted");
Check(layout.Reserve(mutableRequests.AsEnumerable().Reverse().ToArray(), writes.AsEnumerable().Reverse().ToArray()).GeometrySha256 == golden,
    "deterministic input permutation");
mutableIndexes[0] = 10; mutableRequests.Clear(); writes.Clear();
Check(accepted.Reservations[0].RetirementTrueIndexes.SequenceEqual(new[] { 0, 255 }) && accepted.DeclaredWrites.Count == 4 &&
    accepted.GeometrySha256 == golden, "immutable caller snapshots");
NativeLevelSlotRequest[] pair = [Request("stone", 11), Request("town", 13)];
Reject(() => layout.Reserve(pair, [new("steal", "stone", new(wad, townData.Offset, 4))]), "cross-level resource claim");
Reject(() => layout.Reserve(pair, [new("unknown", "missing", new(wad, townData.Offset, 4))]), "unreserved owner");
Reject(() => layout.Reserve(pair, [new("directory", "stone", new(wad, 11 * 8, 8))]), "directory alias mutation");
Reject(() => layout.Reserve(pair, [new("global", "stone", new(exe, 0x47910, 4))]), "unowned native portal consumer");
Reject(() => layout.Reserve(pair, [new("cross", "stone", new(wad, stoneData.EndExclusive - 2, 4))]), "resource boundary crossing");
Reject(() => layout.Reserve(pair, [new("other", "stone", new(new(NativeAddressKind.WadFile,
    wad.OwnerIdentity, NativeAddressRevision.Original, wad.ByteLength), stoneData.Offset, 4))]), "same-label foreign source object");
Reject(() => layout.Reserve(pair, [new("final", "stone", new(new(NativeAddressKind.WadFile,
    wad.OwnerIdentity, NativeAddressRevision.Final, wad.ByteLength), stoneData.Offset, 4))]), "wrong address revision");
Reject(() => layout.Reserve(pair, [new("a", "stone", new(wad, stoneData.Offset, 8)), new("b", "stone", new(wad, stoneData.Offset + 4, 8))]),
    "same-owner overlapping writes");
Reject(() => layout.Reserve(pair, [new("a", "stone", new(wad, stoneData.Offset, 8)), new("a", "town", new(wad, townData.Offset, 8))]),
    "duplicate write IDs");
Reject(() => layout.Reserve(pair, Enumerable.Range(0, 4097).Select(i => new NativeLevelSlotDeclaredWrite("w" + i,
    "stone", new(wad, stoneData.Offset, 1))).ToArray()), "bounded write count");
Check(layout.Reserve(pair, [new("a", "stone", new(wad, stoneData.Offset, 8)), new("b", "stone", new(wad, stoneData.Offset + 8, 8))])
    .DeclaredWrites.Count == 2, "touching writes accepted");
Check(layout.Reserve(pair).Reservations.Count == 2 && accepted.GeometrySha256 == golden, "failures do not consume reservations");

byte[] aliased = header.ToArray();
header.AsSpan(11 * 8, 8).CopyTo(aliased.AsSpan(15 * 8, 8));
Reject(() => new NativeLevelSlotOwnershipLayout(aliased, wad, exe, []), "physical slot alias");
aliased = header.ToArray(); header.AsSpan(11 * 8, 8).CopyTo(aliased.AsSpan(81 * 8, 8));
Reject(() => new NativeLevelSlotOwnershipLayout(aliased, wad, exe, []), "shared non-level resource alias");
byte[] reserved = header.ToArray(); header.AsSpan(11 * 8, 8).CopyTo(reserved.AsSpan(79 * 8, 8));
Reject(() => new NativeLevelSlotOwnershipLayout(reserved, wad, exe, []), "occupied reserved row");
byte[] missing = header.ToArray(); missing.AsSpan(11 * 8, 8).Clear();
Reject(() => new NativeLevelSlotOwnershipLayout(missing, wad, exe, []), "missing retail resource");

byte[] component = PortalComponent();
long componentOffset = Resource(layout.RequireSlot(10), NativeLevelSlotResourceKind.LevelData).Offset + 0x100;
var observed = NativeLevelSlotOwnershipSource.ParsePortalComponent(component, 10, componentOffset, wad);
Check(observed.Count == 1 && observed[0].DestinationLevelId == 11 && observed[0].Descriptor.Offset == componentOffset + 12,
    "native byte-type portal parsing");
var withPortal = new NativeLevelSlotOwnershipLayout(header, wad, exe, observed);
Reject(() => withPortal.Reserve([Request("home", 10)], [new("portal", "home", new(wad, componentOffset + 16, 4))]),
    "portal destination protected");
Reject(() => withPortal.Reserve([Request("home", 10)], [new("kind", "home", new(wad, componentOffset + 12, 1))]),
    "portal type protected");
Reject(() => withPortal.Reserve([Request("stone", 11)], portalIntents: [new(observed[0].Descriptor, "stone")]),
    "intent is not native retarget proof", "not proof of a patched native consumer");
Reject(() => new NativeLevelSlotOwnershipLayout(header, wad, exe, [observed[0], observed[0]]), "duplicate observed portal");
Reject(() => new NativeLevelSlotOwnershipLayout(header, wad, exe, [observed[0] with { DestinationLevelId = 16 }]), "observed portal identity alias");
Reject(() => new NativeLevelSlotOwnershipLayout(header, wad, exe, [observed[0] with { SourceLevelId = 11 }]), "portal resource-owner mismatch");
Reject(() => new NativeLevelSlotOwnershipLayout(header, wad, exe, [observed[0] with { DescriptorSha256 = "invented" }]), "malformed portal evidence");
component[12] = 0;
Check(NativeLevelSlotOwnershipSource.ParsePortalComponent(component, 10, componentOffset, wad).Count == 0, "generic surface param is not destination");
component = PortalComponent(); Put(component, 8, 0);
Reject(() => NativeLevelSlotOwnershipSource.ParsePortalComponent(component, 10, componentOffset, wad), "portal pointer into directory");
component = PortalComponent(); Put(component, 4, 64);
Reject(() => NativeLevelSlotOwnershipSource.ParsePortalComponent(component, 10, componentOffset, wad), "portal count bound");
component = PortalComponent(); Put(component, 0, 20);
Reject(() => NativeLevelSlotOwnershipSource.ParsePortalComponent(component, 10, componentOffset, wad), "portal length preimage");
byte[] shortPortal = PortalComponent()[..20]; Put(shortPortal, 0, 20);
Reject(() => NativeLevelSlotOwnershipSource.ParsePortalComponent(shortPortal, 10, componentOffset, wad), "truncated portal destination record");
byte[] sharedPortal = new byte[32];
Put(sharedPortal, 0, 32); Put(sharedPortal, 4, 2); Put(sharedPortal, 8, 12); Put(sharedPortal, 12, 12);
sharedPortal[16] = 6; Put(sharedPortal, 20, 11);
Reject(() => NativeLevelSlotOwnershipSource.ParsePortalComponent(sharedPortal, 10, componentOffset, wad), "shared descriptor alias");
component = PortalComponent(); Put(component, 8, uint.MaxValue);
Reject(() => NativeLevelSlotOwnershipSource.ParsePortalComponent(component, 10, componentOffset, wad), "overflowing descriptor pointer");
component = PortalComponent(); Put(component, 8, 9);
Reject(() => NativeLevelSlotOwnershipSource.ParsePortalComponent(component, 10, componentOffset, wad), "unaligned descriptor pointer");
Reject(() => NativeLevelSlotOwnershipSource.ParsePortalComponent(new byte[65540], 10, componentOffset, wad), "component allocation budget");
Reject(() => layout.Reserve(pair, [null!]), "null declared write");
Reject(() => layout.Reserve(pair, [new("null", "stone", null!)]), "null extent");
Reject(() => layout.Reserve(Enumerable.Range(0, 36).Select(i => Request("slot" + i, 11)).ToArray()), "bounded reservation count");

// The source-free suite may create only its own tiny disposable input. It must
// not need a retail disc or promote a neutral synthetic layout to source proof.
string tinyPath = Path.Combine(Path.GetTempPath(), "spyro-slot-source-refusal-" + Guid.NewGuid().ToString("N") + ".bin");
try
{
    await File.WriteAllBytesAsync(tinyPath, new byte[32]);
    bool failed = false;
    try { _ = await NativeLevelSlotOwnershipSource.CaptureAsync(tinyPath); }
    catch (InvalidDataException e) { failed = e.Message.Contains("source length", StringComparison.Ordinal); }
    Check(failed && new FileInfo(tinyPath).Length == 32, "tiny synthetic source refuses without mutation");
    refusals++;
}
finally { File.Delete(tinyPath); }

if (args.Length == 2 && args[0] == "--source")
{
    var source = await NativeLevelSlotOwnershipSource.CaptureAsync(args[1]);
    Check(source.SourceImageSha256 == NativeLevelSlotOwnershipSource.ExpectedSourceSha256, "whole original source binding");
    Check(source.Slots.Count == 35 && source.ObservedPortals.Count == 29, "all native slots and portal descriptors");
    Check(source.NativeCertificates.Count == 6, "native consumer/capacity certificates");
    Check(Resource(source.Slots.Single(s => s.LevelId == 11), NativeLevelSlotResourceKind.LevelData).Offset == 0xB93800,
        "exact Stone Hill loaded data, not metadata entry9");
    Check(Resource(source.Slots.Single(s => s.LevelId == 13), NativeLevelSlotResourceKind.LevelData).Offset == 0x119E000,
        "exact Town Square data");
    Check(source.ObservedPortals.Single(p => p.DestinationLevelId == 11).Descriptor.Offset + 4 == 0x927C94,
        "source-discovered Artisans Stone Hill portal destination");
    var all = source.Slots.Select(s => Request("slot" + s.LevelId, s.LevelId, 0, 255)).ToArray();
    var allWrites = source.Slots.SelectMany(s => new[]
    {
        new NativeLevelSlotDeclaredWrite("overlay" + s.LevelId, "slot" + s.LevelId,
            Resource(s, NativeLevelSlotResourceKind.Overlay)),
        new NativeLevelSlotDeclaredWrite("music" + s.LevelId, "slot" + s.LevelId,
            Resource(s, NativeLevelSlotResourceKind.InitialMusicRow))
    }).ToArray();
    var plan = source.Reserve(all, allWrites);
    Check(plan.Ownership.Reservations.Count == 35 && plan.Ownership.DeclaredWrites.Count == 70, "source-bound35-slot composition precondition");
    Check(plan.PlanSha256 == source.Reserve(all.Reverse().ToArray(), allWrites.Reverse().ToArray()).PlanSha256,
        "source-bound deterministic digest");
    Check(!plan.AddsIndependentLevel && !plan.PublishesCandidate && !plan.NativePayloadSemanticsProven &&
        !plan.PortalRetargetingProven && !plan.RuntimeAcceptance, "honest bounded proof flags");
    Check(plan.Ownership.Reservations.All(r => r.Slot.SaveRows.Single(s => s.Kind == "object-retirement").ByteLength == 32) &&
        plan.Ownership.Reservations.SelectMany(r => r.Slot.SaveRows).Max(s => s.SaveOffset + s.ByteLength) == 0x56C,
        "35retail save owners end before reserved index35 bitmap");
    Reject(() => source.Reserve([Request("lab", 65)]), "source-boundID65 blocked");
    Reject(() => source.Reserve([Request("home", 10)], [new("bad", "home", source.ObservedPortals[0].Descriptor)]),
        "source-bound native portal mutation blocked");
    Check(plan.PlanSha256 == "c7406bfce9e60e346f5d488b01a68a2c35af7491ab4e5b7f4249e9f386c41a32", "native ownership-plan golden");
    Console.WriteLine($"NATIVE_LEVEL_SLOT_OWNERSHIP_SOURCE_OK slots=35 portals=29 declarations=70 plan={plan.PlanSha256} source={source.SourceImageSha256} publication=false runtime=false");
}
else if (args.Length != 0) throw new ArgumentException("Usage: [--source <original.bin>]");
Console.WriteLine($"NATIVE_LEVEL_SLOT_OWNERSHIP_SMOKE_OK checks={checks} refusals={refusals} publication=false runtime=false");

static void Put(byte[] bytes, int offset, uint value) => BinaryPrimitives.WriteUInt32LittleEndian(bytes.AsSpan(offset), value);
static byte[] FixtureHeader()
{
    byte[] result = new byte[0x800];
    for (int i = 0; i < 83; i++)
    {
        if (i is 79 or 80) continue;
        Put(result, i * 8, (uint)(0x800 + i * 0x1000)); Put(result, i * 8 + 4, 0x1000);
    }
    return result;
}
static byte[] PortalComponent()
{
    byte[] result = new byte[24];
    Put(result, 0, 24); Put(result, 4, 1); Put(result, 8, 8);
    result[12] = 6; result[13] = 99; // Upper type-word bytes are not the native byte discriminator.
    Put(result, 16, 11); Put(result, 20, 0); return result;
}
