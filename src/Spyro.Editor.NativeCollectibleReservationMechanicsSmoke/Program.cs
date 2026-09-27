using System.Buffers.Binary;
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
const uint Stop = 0x80010000, ExeBase = 0x8000F800, Fc = 0x800785FC;
var manifest = DetachedCollectibleManifest.CreateNew(Guid.Parse("12345678-1234-5678-9012-123456789abc"),
    [Guid.Parse("11111111-1111-1111-1111-111111111111"), Guid.Parse("22222222-2222-2222-2222-222222222222")]);
var original = NativeCollectibleReservationMechanics.MeasureReserve(0x800);
var proposed = NativeCollectibleReservationMechanics.MeasureReserve(0xA00);
Check(original.AllocationCeiling == 0x801FF800 && proposed.AllocationCeiling == 0x801FF600 &&
    proposed.PeteResidentBase == 0x801C59B8 && proposed.NormalFloor == 0x801879B0, "independent shifted allocation goldens");
Check(proposed.StartupCdStart == 0x801BF600 && proposed.StartupCdEnd == 0x801FF600 &&
    proposed.PeteTemporaryStart == 0x8018B600 && proposed.PeteReadEnd == 0x801C5600, "full CD request boundaries");
Check(proposed.PeteCopySource == 0x8018BE00 && proposed.PeteCopySourceEnd == 0x801C5A48 &&
    proposed.PeteActualCopyReadEnd == 0x801C5A50 && proposed.PeteReadOverhang == 0x450 && proposed.PeteActualReadOverlap == 0x98,
    "native overread and forward overlap remain explicit");
Check(proposed.BootStackPointer == 0x801FFFF8 && original.BootStackPointer == proposed.BootStackPointer &&
    original.HeapByteLength == proposed.HeapByteLength + 0x200 && proposed.HeapByteLength == 0x184BC0, "independent stack word and shifted heap");
foreach (uint reserve in new uint[] { 0, 0x7FF, 0x801, 0x900, 0x1000, uint.MaxValue })
    Refuse(() => NativeCollectibleReservationMechanics.MeasureReserve(reserve), "closed two-reserve arithmetic " + reserve);
Refuse(() => NativeCollectibleReservationMechanics.Create(NativeCollectibleSelectedSlotResidence.Describe(manifest, 0)),
    "neutral descriptor cannot issue source-bound afterimage");
Refuse(() => NativeCollectibleReservationMechanics.Create(null!), "missing residence");
if (args.Length == 0)
{
    Console.WriteLine($"COLLECTIBLE_RESERVATION_MECHANICS_SOURCE_FREE_OK checks={checks} sourceReads=0 nativeResidenceIssued=false");
    return;
}
if (args.Length != 2 || args[0] != "--source") throw new ArgumentException("Usage: no args or --source <original-USA-BIN>");
using var usa = NativeRegionalSourceSession.Open(args[1], NativeDiscRevision.UsaScus94228);
var residence = NativeCollectibleSelectedSlotResidence.Capture(usa, manifest, 0);
var plan = NativeCollectibleReservationMechanics.Create(residence);
byte[] before = residence.CopyOriginalExecutable(), after = plan.CopyExecutable();
Check(plan.SourceEvidenceBound && ReferenceEquals(plan.Residence, residence) && plan.OriginalGeometry == original && plan.ProposedGeometry == proposed,
    "same source-bound residence ancestry and geometry");
Check(!plan.NativeResidenceIssued && !plan.Installed && !plan.LifetimeProven && !plan.WholeEngineFitProven &&
    !plan.CardIoApproved && !plan.PublicationApproved, "mechanics cannot issue installation/lifetime/card approval");
Refuse(plan.RequireNativeInstallation, "plan cannot install anything");
Check(plan.ReserveBeforeimage.Offset == 0x65DA4 && plan.ReserveBeforeimage.ByteLength == 4 &&
    plan.ReserveAfterimage.Offset == 0x65DA4 && plan.ReserveAfterimage.ByteLength == 4, "exact typed four-byte field owners");
Check(BinaryPrimitives.ReadUInt32LittleEndian(before.AsSpan(0x65DA4)) == 0x800 &&
    BinaryPrimitives.ReadUInt32LittleEndian(after.AsSpan(0x65DA4)) == 0xA00 &&
    before.AsSpan(0, 0x65DA4).SequenceEqual(after.AsSpan(0, 0x65DA4)) && before.AsSpan(0x65DA8).SequenceEqual(after.AsSpan(0x65DA8)),
    "complete executable complement unchanged including independent stack word");
Check(plan.RevertExact(after).SequenceEqual(before) && plan.CopyOriginalExecutable().SequenceEqual(before) &&
    plan.ApplyToExactOriginal(before).SequenceEqual(after), "exact original/afterimage/inverse round trip");
Check(plan.Evidence.Count == residence.Evidence.Count + 1 && plan.Evidence.Any(s => s.Pc == 0x80016914 && s.ByteLength == 0x1C),
    "reuse residence pins plus actually executed native clear helper");
foreach (int at in new[] { 0, 0x800, 0x7114, 0x4BEF8, 0x4C17C, 0x65DA3, 0x65DA4, 0x65DA5, 0x65DA6, 0x65DA7, 0x65DA8, 0x65FFF })
{
    byte[] b = before.ToArray(); b[at] ^= 1;
    Refuse(() => plan.ApplyToExactOriginal(b), "modified original/complement " + at);
    b = after.ToArray(); b[at] ^= 1;
    Refuse(() => plan.RevertExact(b), "modified afterimage/complement " + at);
}
Refuse(() => plan.ApplyToExactOriginal(before[..^1]), "truncated original");
Refuse(() => plan.ApplyToExactOriginal([.. before, 0]), "extended original");
Refuse(() => plan.RevertExact(after[..^1]), "truncated afterimage");
Refuse(() => plan.RevertExact([.. after, 0]), "extended afterimage");
Refuse(() => plan.RevertExact(before), "unapplied original is not afterimage");
byte[] leak = plan.CopyExecutable(); leak[0] ^= 1;
Check(plan.CopyExecutable().SequenceEqual(after), "afterimage only exposed by copy");
leak = plan.CopyOriginalExecutable(); leak[0] ^= 1;
Check(plan.CopyOriginalExecutable().SequenceEqual(before), "original only exposed by copy");

RenderProbeCpu Cpu(byte[] exe)
{
    var cpu = new RenderProbeCpu { StrictSignedArithmetic = true };
    cpu.Copy(ExeBase, exe); cpu.Regs[29] = 0x801FFFF8; cpu.Regs[31] = Stop;
    return cpu;
}
byte[] Snapshot(RenderProbeCpu cpu, uint address, int count) => Enumerable.Range(0, count).Select(i => (byte)cpu.Get(address + (uint)i, 1)).ToArray();
byte[] poison = Enumerable.Range(0, 0x200).Select(i => (byte)(0x31 + i * 17)).ToArray();
// All seven reserve readers are executed from their real native instruction
// windows. Stops at hardware/BIOS/other service entry never claim service success.
foreach (var (exe, geometry) in new[] { (before, original), (after, proposed) })
{
    var cpu = Cpu(exe); cpu.Copy(proposed.AllocationCeiling, poison);
    cpu.Run(0x8005B904, 0x8005DB14);
    Check(cpu.Regs[29] == geometry.BootStackPointer && cpu.Get(0x800730C4) == geometry.HeapByteLength &&
        cpu.Get(0x800730C0) == 0x8007AA38 && cpu.Regs[4] == 0x8007AA3C, "native boot stack/heap argument and delay slot");
    Check(Snapshot(cpu, proposed.AllocationCeiling, 0x200).SequenceEqual(poison), "boot setup does not initialize the proposed ledger");
    cpu = Cpu(exe); cpu.Regs[29] = 0x801FFE00;
    cpu.Regs[17] = 0x80100000; cpu.Regs[19] = 0x80100004; cpu.Regs[18] = 0x80100008;
    cpu.Put(0x80100000, 37); cpu.Put(0x80100004, 0x4000); cpu.Put(0x80100008, 0x123000);
    cpu.Run(0x80012948, 0x80016500);
    Check(cpu.Regs[4] == 37 && cpu.Regs[5] == geometry.StartupCdStart && cpu.Regs[6] == 0x40000 &&
        cpu.Regs[7] == 0x127000 && cpu.Regs[5] + cpu.Regs[6] == geometry.StartupCdEnd, "startup CD caller arguments only");
    foreach (uint start in new uint[] { 0x80012C6C, 0x80033080, 0x80033250 })
    {
        cpu = Cpu(exe); cpu.Regs[2] = 3; cpu.Regs[3] = 3;
        cpu.Run(start, start == 0x80012C6C ? 0x8005B7D8u : 0x8005B6F8u);
        Check(cpu.Get(Fc) == geometry.AllocationCeiling && (start == 0x80012C6C || cpu.Regs[4] == 1),
            "real reset ceiling and native call delay at " + start.ToString("X8"));
    }
    cpu = Cpu(exe); cpu.Put(0x8007A714, 0x3A000); cpu.Put(0x8007A710, 0x7B8800); cpu.Put(0x80076B90, 37);
    cpu.Run(0x8005B7D8, 0x80016500);
    Check(cpu.Regs[4] == 37 && cpu.Regs[5] == geometry.PeteTemporaryStart && cpu.Regs[6] == 0x3A000 &&
        cpu.Regs[7] == 0x7B8800 && cpu.Regs[5] + cpu.Regs[6] == geometry.PeteReadEnd, "PETE directory-length CD setup");
    // Synthetic header supplies the previously source-validated payload length;
    // no CD/BIOS return, payload contents or helper result is fabricated.
    cpu.Put(geometry.PeteTemporaryStart, 0x39C48);
    cpu.Run(0x8005B844, 0x80016958);
    Check(cpu.Get(Fc) == geometry.PeteResidentBase && cpu.Regs[4] == geometry.PeteResidentBase &&
        cpu.Regs[5] == geometry.PeteCopySource && cpu.Regs[6] == 0x39C48, "second PETE reserve reader and exact native copy arguments");
}

// Real allocator plus real native clear helper for both FC phases and both
// a0 modes. Counterfactual cross-combinations are arithmetic tests, not routes.
foreach (uint fc in new[] { proposed.PeteResidentBase, proposed.AllocationCeiling })
foreach (uint mode in new uint[] { 0, 1 })
{
    var cpu = Cpu(after); cpu.Put(Fc, fc); cpu.Regs[4] = mode;
    cpu.Copy(proposed.AllocationCeiling, poison);
    cpu.Copy(fc - 0x600C, Enumerable.Repeat((byte)0xA7, 0x4010).ToArray());
    cpu.Run(0x8005B6F8, Stop);
    uint stride = mode == 0 ? 0x1C000u : 0x13000u;
    uint f0 = fc - 0x6008, f4 = fc - 0x6000, f8 = fc - 0x2000, ec = f0 - stride, e8 = ec - stride;
    Check(cpu.Get(0x800785F8) == f8 && cpu.Get(0x800785F4) == f4 && cpu.Get(0x800785F0) == f0 &&
        cpu.Get(0x800785EC) == ec && cpu.Get(0x800785E8) == e8, "native FC-derived allocation pointers");
    Check(cpu.Get(0x80076F50) == e8 && cpu.Get(0x80076FD4) == ec && cpu.Get(0x80076F58) == f0 &&
        cpu.Get(0x80076FDC) == f0 && cpu.Get(0x80076F54) == f4 && cpu.Get(0x80076FD8) == f4, "both native render control sets");
    Check(Snapshot(cpu, f0, 0x4008).All(b => b == 0) && cpu.Get(f0 - 4) == 0xA7A7A7A7 && cpu.Get(f8) == 0xA7A7A7A7,
        "actual helper clears exactly F0/8 and F4/4000");
    HashSet<uint> globals = new uint[] { 0x800785F8, 0x800785F4, 0x800785F0, 0x800785EC, 0x800785E8,
        0x80076F50, 0x80076FD4, 0x80076F58, 0x80076FDC, 0x80076F54, 0x80076FD8 }.SelectMany(p => Enumerable.Range(0, 4).Select(i => p + (uint)i)).ToHashSet();
    Check(cpu.Writes.Count == 0x4038 && cpu.Writes.All(p => globals.Contains(p) || p >= f0 && p < f8 || p >= 0x801FFFF0 && p < 0x801FFFF4),
        "complete allocator/native-clear write footprint");
    Check(cpu.Calls.SequenceEqual(new uint[] { 0x80016914, 0x80016914 }) && cpu.Regs[29] == 0x801FFFF8 && cpu.Regs[31] == Stop,
        "actual clear calls and allocator frame return");
    Check(Snapshot(cpu, proposed.AllocationCeiling, 0x200).SequenceEqual(poison), "allocator retains every guard and ledger byte");
    cpu.Writes.Clear(); cpu.Calls.Clear(); cpu.Run(0x8001441C, 0x80058B68);
    Check(cpu.Get(0x80075824) == f8 && cpu.Get(0x80075738) == f8 && cpu.Get(f8 + 1, 1) == 0xFF && cpu.Get(fc) == uint.MaxValue,
        "particle globals/free marker/sentinel native delay store");
    var particleWrites = new uint[] { 0x80075824, 0x80075738, fc }.SelectMany(p => Enumerable.Range(0, 4).Select(i => p + (uint)i)).Append(f8 + 1).ToHashSet();
    Check(cpu.Writes.Count == 13 && cpu.Writes.ToHashSet().SetEquals(particleWrites), "exact thirteen particle-init write bytes");
    byte[] expected = poison.ToArray(); if (fc == proposed.AllocationCeiling) expected.AsSpan(0, 4).Fill(0xFF);
    Check(Snapshot(cpu, proposed.AllocationCeiling, 0x200).SequenceEqual(expected), "only alternate FC sentinel may touch first four guard bytes");
}

// Execute the complete native forward copy over synthetic initial RAM. Break
// at retired-instruction boundaries per full group to respect the unchanged
// interpreter's per-Run budget; registers, RAM and original instructions persist.
{
    var cpu = Cpu(after); cpu.Copy(proposed.AllocationCeiling, poison);
    cpu.Copy(proposed.PeteCopySource, Enumerable.Range(0, 0x39C50).Select(i => (byte)(i * 29 + 7)).ToArray());
    cpu.Regs[4] = proposed.PeteResidentBase; cpu.Regs[5] = proposed.PeteCopySource; cpu.Regs[6] = 0x39C48;
    cpu.Run(0x80016958, 0x80016960);
    for (int i = 0; i < 0x39C4; i++)
    {
        cpu.Run(0x80016960, 0x80016998);
        cpu.Run(0x80016998, 0x80016960);
    }
    cpu.Run(0x80016960, Stop);
    Check(cpu.Writes.Count == 0x39C48 && cpu.Writes.Distinct().Count() == 0x39C48 &&
        cpu.Writes.Min() == proposed.PeteResidentBase && cpu.Writes.Max() + 1 == proposed.AllocationCeiling,
        "full real forward copy writes exactly payload length below guard");
    Check(cpu.Reads.Count == 0x39C50 / 4 && cpu.Reads.Min() == proposed.PeteCopySource && cpu.Reads.Max() + 4 == proposed.PeteActualCopyReadEnd &&
        cpu.Regs[4] == proposed.AllocationCeiling && cpu.Regs[5] == proposed.PeteActualCopyReadEnd, "full native copy including final eight-byte read-ahead");
    Check(Snapshot(cpu, proposed.AllocationCeiling, 0x200).SequenceEqual(poison), "native copy preserves complete proposed reservation");
    Check(cpu.Calls.Count == 0 && cpu.Regs[29] == 0x801FFFF8, "native leaf copy has no invented helpers or stack");
}
Check(plan.CopyOriginalExecutable().SequenceEqual(before) && plan.CopyExecutable().SequenceEqual(after) &&
    usa.ReadExecutableRange(0, usa.ExecutableByteLength).SequenceEqual(before), "all CPU/afterimage tests preserve actual original executable");
usa.VerifyUnchanged();
Console.WriteLine($"COLLECTIBLE_RESERVATION_MECHANICS_SOURCE_OK checks={checks} allocatorCases=4 reserveReaders=7 fullNativeCopyBytes=39C48 imageWrites=0 cardWrites=0 nativeResidenceIssued=false");
