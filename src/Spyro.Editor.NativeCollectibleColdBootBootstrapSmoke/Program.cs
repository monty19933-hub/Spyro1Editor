using System.Buffers.Binary;
using Spyro.Editor.Core.Analysis;
using Spyro.Editor.Core.Exporting;
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
uint U32(byte[] bytes, int offset) => BinaryPrimitives.ReadUInt32LittleEndian(bytes.AsSpan(offset));
const uint StubPc = 0x80075640, OriginalEntry = 0x8005B8E0, Ledger = 0x801FF600, LedgerEnd = 0x801FF800;
NativeCollectibleColdBootBootstrap.RequireStubExtent(StubPc, 0x2C); checks++;
NativeCollectibleColdBootBootstrap.RequireStubExtent(StubPc, 0x1C0); checks++;
foreach (var (pc, length) in new (uint, int)[] { (StubPc, 0), (StubPc, -4), (StubPc, int.MaxValue),
    (StubPc, 0x1C4), (StubPc + 1, 0x2C), (StubPc, 0x2D), (StubPc - 4, 0x2C),
    (0x800757FC, 8), (0x80075800, 4), (OriginalEntry, 0x2C), (Ledger, 0x2C), (0xFFFFFFFC, 8) })
    Refuse(() => NativeCollectibleColdBootBootstrap.RequireStubExtent(pc, length), "unaligned/oversized/overlapping/unowned stub extent");
Refuse(() => NativeCollectibleColdBootBootstrap.Create(null!), "missing reservation ancestry");
Refuse(() => NativeCollectibleColdBootBootstrap.RequireOriginalSource(new byte[0x66000]), "fabricated original source");
if (args.Length == 0)
{
    Console.WriteLine($"COLLECTIBLE_COLD_BOOT_BOOTSTRAP_SOURCE_FREE_OK checks={checks} sourceReads=0 installed=false");
    return;
}
if (args.Length != 2 || args[0] != "--source") throw new ArgumentException("Usage: no args or --source <original-USA-BIN>");
var manifest = DetachedCollectibleManifest.CreateNew(Guid.Parse("12345678-1234-5678-9012-123456789abc"),
    [Guid.Parse("11111111-1111-1111-1111-111111111111"), Guid.Parse("22222222-2222-2222-2222-222222222222")]);
using var usa = NativeRegionalSourceSession.Open(args[1], NativeDiscRevision.UsaScus94228);
var residence = NativeCollectibleSelectedSlotResidence.Capture(usa, manifest, 0);
var mechanics = NativeCollectibleReservationMechanics.Create(residence);
var plan = NativeCollectibleColdBootBootstrap.Create(mechanics);
byte[] original = mechanics.CopyOriginalExecutable(), reserved = mechanics.CopyExecutable(), proposed = plan.CopyExecutable();
Check(ReferenceEquals(plan.Reservation, mechanics) && plan.SourceEvidenceBound && plan.EntryPc == StubPc &&
    plan.OriginalEntryPc == OriginalEntry && plan.LedgerStart == Ledger && plan.LedgerEnd == LedgerEnd && plan.StubByteLength == 0x2C,
    "exact paired reservation ancestry and ephemeral layout");
Check(!plan.BiosLoadProven && !plan.InstructionCacheProven && !plan.HandoffBufferOwnershipProven && !plan.InterruptSafetyProven &&
    !plan.NativeResidenceIssued && !plan.Installed && !plan.LifetimeProven && !plan.WholeEngineFitProven &&
    !plan.CardIoApproved && !plan.PublicationApproved && !plan.PersistentCodeIssued, "all external loader/owner/lifetime gates remain false");
Refuse(plan.RequireNativeInstallation, "prototype cannot install itself");
Check(U32(original, 0x10) == OriginalEntry && U32(original, 0x14) == 0 && U32(original, 0x18) == 0x80010000 &&
    U32(original, 0x1C) == 0x65800 && U32(original, 0x28) == 0 && U32(original, 0x2C) == 0 &&
    U32(original, 0x30) == 0x801FFFF0 && U32(original, 0x34) == 0 &&
    original.AsSpan(0x65E40, 0x1C0).IndexOfAnyExcept((byte)0) < 0, "entire actual loaded/cleared tail and loader header");
Check(U32(proposed, 0x10) == StubPc && U32(proposed, 0x65DA4) == 0xA00 && U32(proposed, 0x65DA8) == 0x200000,
    "new entry is paired with A00 without changing stack origin");
Check(plan.EntryField.Offset == 0x10 && plan.EntryField.ByteLength == 4 && plan.TailOwner.Offset == 0x65E40 &&
    plan.TailOwner.ByteLength == 0x1C0 && plan.StubOwner.Offset == 0x65E40 && plan.StubOwner.ByteLength == 0x2C,
    "typed entry field, complete ephemeral tail and actual stub extents");
Check(Enumerable.Range(0, proposed.Length).Where(i => i is not (>= 0x10 and < 0x14) && i is not (>= 0x65E40 and < 0x66000))
    .All(i => proposed[i] == reserved[i]), "every byte outside entry/tail preserves the paired ancestor");
Check(plan.RevertToReservationExact(proposed).SequenceEqual(reserved) && plan.RevertExact(proposed).SequenceEqual(original) &&
    plan.ApplyToExactReservation(reserved).SequenceEqual(proposed), "exact two-step inverse reaches complete original source");
Check(plan.CopyStub().Select((_, i) => i).Where(i => i % 4 == 0).Select(i => U32(plan.CopyStub(), i)).SequenceEqual(new uint[] {
    0x3C02801F, 0x3442F600, 0x3C03801F, 0x3463F800, 0xAC400000, 0x24420004,
    0x0043082B, 0x1420FFFC, 0, 0x08016E38, 0 }), "independent exact register/branch/jump instruction encoding");
foreach (int at in new[] { 0, 0x10, 0x14, 0x18, 0x1C, 0x28, 0x2C, 0x30, 0x34, 0x800, 0x4C0E0,
    0x65DA4, 0x65DA5, 0x65DA8, 0x65E3F, 0x65E40, 0x65E64, 0x65E6C, 0x65FFF })
{
    byte[] bad = original.ToArray(); bad[at] ^= 1;
    Refuse(() => NativeCollectibleColdBootBootstrap.RequireOriginalSource(bad), "original header/tail/clear/complement change");
    bad = reserved.ToArray(); bad[at] ^= 1;
    Refuse(() => plan.ApplyToExactReservation(bad), "changed paired reservation source");
    bad = proposed.ToArray(); bad[at] ^= 1;
    Refuse(() => plan.RevertExact(bad), "changed final entry/reserve/tail/complement");
}
Refuse(() => plan.ApplyToExactReservation(original), "unpaired original800 reserve");
Refuse(() => plan.ApplyToExactReservation(reserved[..^1]), "truncated reserved executable");
Refuse(() => plan.ApplyToExactReservation([.. reserved, 0]), "extended reserved executable");
Refuse(() => plan.RevertExact(proposed[..^1]), "truncated final executable");
Refuse(() => plan.RevertExact([.. proposed, 0]), "extended final executable");
byte[] detached = plan.CopyExecutable(); detached[0] ^= 1;
Check(plan.CopyExecutable().SequenceEqual(proposed), "final executable defensive copy");
detached = plan.CopyStub(); detached[0] ^= 1;
Check(!detached.SequenceEqual(plan.CopyStub()), "stub defensive copy");

byte[] poison = Enumerable.Range(0, 0x200).Select(i => (byte)(i * 13 + 3)).ToArray();
byte[] Snapshot(RenderProbeCpu cpu, uint address, int count) => Enumerable.Range(0, count).Select(i => (byte)cpu.Get(address + (uint)i, 1)).ToArray();
RenderProbeCpu LoadDeclaredText(byte[] executable, uint salt)
{
    var cpu = new RenderProbeCpu { StrictSignedArithmetic = true };
    cpu.Copy(0x80075640, Enumerable.Repeat((byte)0xA7, 0x53F8).ToArray());
    // Simulate the header's declared transfer, not execution of a BIOS loader.
    cpu.Copy(U32(executable, 0x18), executable.AsSpan(0x800, checked((int)U32(executable, 0x1C))).ToArray());
    cpu.Copy(Ledger - 4, Enumerable.Repeat((byte)0xB9, 0x208).ToArray()); cpu.Copy(Ledger, poison);
    for (int i = 1; i < 32; i++) cpu.Regs[i] = salt ^ (0xA1230000u + (uint)i * 0x10203);
    cpu.Hi = salt ^ 0x12345678; cpu.Lo = salt ^ 0x9ABCDEF0;
    return cpu;
}
foreach (uint salt in new uint[] { 0, 0xFFFFFFFF, 0x55AA77CC })
{
    var baseline = LoadDeclaredText(original, salt); var cpu = LoadDeclaredText(proposed, salt);
    uint[] entryRegisters = cpu.Regs.ToArray(); uint hi = cpu.Hi, lo = cpu.Lo;
    cpu.Run(U32(proposed, 0x10), OriginalEntry);
    Check(cpu.Writes.Count == 0x200 && cpu.Writes.Distinct().Count() == 0x200 &&
        cpu.Writes.Min() == Ledger && cpu.Writes.Max() == LedgerEnd - 1 &&
        Snapshot(cpu, Ledger, 0x200).All(b => b == 0) && cpu.Get(Ledger - 4) == 0xB9B9B9B9 && cpu.Get(LedgerEnd) == 0xB9B9B9B9,
        "exact full200 zero write set and outer guards");
    Check(cpu.Reads.Count == 0 && cpu.Calls.Count == 0 && cpu.Hi == hi && cpu.Lo == lo &&
        Enumerable.Range(0, 32).Where(i => i is not (1 or 2 or 3)).All(i => cpu.Regs[i] == entryRegisters[i]),
        "stub uses only v0/v1/at; no load/helper/stack/HI/LO/entry-state dependency");
    Check(Snapshot(cpu, StubPc, 0x2C).SequenceEqual(plan.CopyStub()), "stub exits before any source-BSS erasure");
    cpu.Writes.Clear(); baseline.Run(OriginalEntry, 0x8005B904); cpu.Run(OriginalEntry, 0x8005B904);
    Check(cpu.Writes.Count == 0x53F8 && cpu.Writes.Distinct().Count() == 0x53F8 &&
        cpu.Writes.Min() == StubPc && cpu.Writes.Max() == 0x8007AA37 &&
        Snapshot(cpu, StubPc, 0x53F8).All(b => b == 0), "original BSS loop erases entire ephemeral tail with exact store extent");
    Check(cpu.Regs.SequenceEqual(baseline.Regs) && cpu.Hi == baseline.Hi && cpu.Lo == baseline.Lo &&
        Snapshot(cpu, StubPc, 0x53F8).SequenceEqual(Snapshot(baseline, StubPc, 0x53F8)), "original/proposed state agrees after original clear");
    Check(Snapshot(cpu, Ledger, 0x200).All(b => b == 0) && Snapshot(baseline, Ledger, 0x200).SequenceEqual(poison),
        "only bootstrap initializes ledger; original BSS loop leaves it alone");
    cpu.Run(0x8005B904, 0x8005DB14); baseline.Run(0x8005B904, 0x8005DB14);
    Check(cpu.Get(StubPc) == entryRegisters[31] && Snapshot(cpu, StubPc + 4, 0x1BC).All(b => b == 0) &&
        Snapshot(cpu, StubPc, 0x1C0).SequenceEqual(Snapshot(baseline, StubPc, 0x1C0)), "original incoming-RA save replaces first erased word identically");
    Check(cpu.Regs[29] == 0x801FFFF8 && cpu.Regs[28] == 0x80075264 && cpu.Regs[4] == 0x8007AA3C &&
        cpu.Regs[31] == 0x8005B970 && cpu.Regs[5] == mechanics.ProposedGeometry.HeapByteLength &&
        cpu.Get(0x800730C4) == mechanics.ProposedGeometry.HeapByteLength, "next real call boundary includes native delay-slot argument");
    Check(Enumerable.Range(0, 32).Where(i => i is not (3 or 5)).All(i => cpu.Regs[i] == baseline.Regs[i]) &&
        baseline.Regs[3] == 0x800 && cpu.Regs[3] == 0xA00 && baseline.Regs[5] == cpu.Regs[5] + 0x200 &&
        cpu.Hi == baseline.Hi && cpu.Lo == baseline.Lo, "only paired reserve/heap values differ at next call boundary");
    var space = new NativeAddressSpace(NativeAddressKind.LevelData, "test-only-zero-ledger-refusal", NativeAddressRevision.Final, 0x2000);
    var kernel = NativeCollectibleCollectionKernel.Compile(manifest, 0, new(space, 0, 0x2000), 0x800E0000, Ledger);
    cpu.Copy(kernel.EntryPc, kernel.CopyBytes()); cpu.Regs[4] = Ledger; cpu.Regs[5] = 0; cpu.Regs[31] = 0x80010000;
    cpu.Writes.Clear(); cpu.Run(kernel.EntryPc, 0x80010000);
    Check(cpu.Regs[2] == (uint)NativeCollectibleCollectionResult.InvalidLedger && cpu.Writes.Count == 0 &&
        Snapshot(cpu, Ledger, 0x200).All(b => b == 0), "zero ledger remains NOT-LOADED, never forged ready or card state");
}
Check(mechanics.OriginalGeometry.StartupCdStart < Ledger && mechanics.OriginalGeometry.StartupCdEnd == LedgerEnd &&
    mechanics.ProposedGeometry.StartupCdEnd == Ledger, "original800 CD request overlaps ledger; pairedA00 ends before it");
Check(plan.CopyExecutable().SequenceEqual(proposed) && mechanics.CopyExecutable().SequenceEqual(reserved) &&
    usa.ReadExecutableRange(0, usa.ExecutableByteLength).SequenceEqual(original), "all prototype and CPU work preserves source and ancestor");
usa.VerifyUnchanged();
Console.WriteLine($"COLLECTIBLE_COLD_BOOT_BOOTSTRAP_SOURCE_OK checks={checks} stubBytes=2C clearedBytes=200 entryStateCases=3 biosExecuted=false installed=false imageWrites=0 cardWrites=0");
