using System.Buffers.Binary;
using System.Security.Cryptography;
using Spyro.Editor.Core.Analysis;
using Spyro.Editor.Core.Exporting;

int checks = 0;
void Check(bool value, string why) { checks++; if (!value) throw new Exception(why); }
void Refuse(Action action, string why)
{ try { action(); } catch (InvalidDataException) { checks++; return; } throw new Exception("Expected refusal: " + why); }
static uint Word(byte[] bytes, int at) => BinaryPrimitives.ReadUInt32LittleEndian(bytes.AsSpan(at));
static void Put(byte[] bytes, int at, uint value) => BinaryPrimitives.WriteUInt32LittleEndian(bytes.AsSpan(at), value);
static string Hash(byte[] bytes) => Convert.ToHexStringLower(SHA256.HashData(bytes));
static byte[] Fixture()
{
    byte[] b = new byte[0x3000]; int cursor = 0x800;
    for (int i = 0; i < 5; i++) { Put(b, i * 8, (uint)cursor); Put(b, i * 8 + 4, 0x800); cursor += 0x800; }
    for (int i = 0x800; i < 0x1000; i++) b[i] = (byte)(i * 7 + 1);
    for (int i = 0x1800; i < 0x2000; i++) b[i] = (byte)(i * 17 + 3);
    for (int i = 0x2800; i < b.Length; i++) b[i] = (byte)(i * 31 + 5);
    Put(b, 0x50, 0x1800); Put(b, 0x54, 0x1804); BinaryPrimitives.WriteUInt16LittleEndian(b.AsSpan(0x152), 7);
    for (int i = 0; i < 6; i++) Put(b, 0x1000 + i * 4, 4);
    Put(b, 0x1018, 0); Put(b, 0x101C, 4); Put(b, 0x1020, 4);
    cursor = 0x2088;
    for (int i = 0; i < 7; i++) { Put(b, cursor, 8); cursor += 8; }
    Put(b, cursor, 0x78); Put(b, cursor + 4, 0);
    return b;
}
void Verify(NativeJapaneseSignFontResidentPlan plan, byte[] before, byte[] atlas)
{
    byte[] after = plan.CopyLevelData();
    Check(after.Length == before.Length + 0x14000, "exact sector-aligned allocation growth");
    Check(Word(after, 0x14) == Word(before, 0x14) + 0x14000, "complete normal model CD read grows by both owners");
    Check(after.AsSpan(0x50, 0x180).SequenceEqual(before.AsSpan(0x50, 0x180)), "all registered roots and classes unchanged");
    Check(plan.Owners.Count == 2 && plan.Atlas.Role == NativeJapaneseFontResidentRole.ImmutableAtlas &&
        plan.Backup.Role == NativeJapaneseFontResidentRole.MutableModalBackup, "distinct immutable/mutable semantic roles");
    Check(plan.Atlas.FinalExtent.Offset == Word(before, 0x10) + Word(before, 0x14) &&
        plan.Atlas.FinalExtent.EndExclusive == plan.Backup.FinalExtent.Offset && plan.Backup.FinalExtent.EndExclusive == Word(after, 0x18),
        "no inferred padding, holes or owner overlap");
    Check(plan.Owners.All(o => ReferenceEquals(o.FinalExtent.Space, plan.FinalDomain) && o.FinalExtent.ByteLength == 0xA000), "actual typed final domain");
    Check(plan.CopyOwnerBytes(NativeJapaneseFontResidentRole.ImmutableAtlas).SequenceEqual(atlas), "every source atlas byte retained");
    Check(!plan.CopyOwnerBytes(NativeJapaneseFontResidentRole.MutableModalBackup).AsSpan().ContainsAnyExcept((byte)0), "explicit complete backup initialization");
    Check(plan.Atlas.InitialBytesSha256 == Hash(atlas) && plan.Backup.InitialBytesSha256 == Hash(new byte[0xA000]), "independent owner hashes");
    HashSet<int> changedHeader = [.. Enumerable.Range(0x14, 4)];
    for (int i = 0; i < 10; i++)
    {
        int n = (int)Word(before, i * 8 + 4); if (n == 0) continue;
        int a = (int)Word(before, i * 8), z = (int)Word(after, i * 8);
        Check(after.AsSpan(z, n).SequenceEqual(before.AsSpan(a, n)), "all old subfile bytes retained " + i);
        var mapped = plan.Layout.Map(new(plan.OriginalDomain, a, n));
        Check(mapped.Final.Offset == z && mapped.Final.ByteLength == n, "old owner maps without crossing insertion " + i);
        if (i > 2) changedHeader.UnionWith(Enumerable.Range(i * 8, 4));
    }
    Check(Enumerable.Range(0, 0x800).Where(i => !changedHeader.Contains(i)).All(i => before[i] == after[i]), "all header complements unchanged");
    Check(plan.Layout.Regions.Count(r => r.Original is null) == 2, "only two explicit new physical owners");
    Check(plan.Memory.FinalSceneStart - plan.Memory.OriginalSceneStart == 0x14000 &&
        plan.Memory.FinalSceneEnd - plan.Memory.OriginalSceneEnd == 0x14000 &&
        plan.Memory.OriginalNormalGap - plan.Memory.FinalNormalGap == 0x14000, "actual full model/scene charging");
    Check(plan.Atlas.ModeledNormalRamStart == plan.Memory.OriginalSceneStart &&
        plan.Atlas.ModeledNormalRamEnd == plan.Backup.ModeledNormalRamStart &&
        plan.Backup.ModeledNormalRamEnd == plan.Memory.FinalSceneStart, "normal RAM projection partitions new read range");
    Check(plan.RevertExact(after).SequenceEqual(before), "complete inverse recovers exact original");
    plan.RequireCurrentBody(before); plan.RequireFinal(after); checks += 2;
    int[] changed = [0, 0x14, 0x18, 0x50, 0x150, 0x7FF, 0x800, (int)Word(before, 0x10),
        (int)plan.Atlas.FinalExtent.Offset, (int)plan.Atlas.FinalExtent.EndExclusive - 1,
        (int)plan.Backup.FinalExtent.Offset, (int)plan.Backup.FinalExtent.EndExclusive - 1,
        (int)Word(after, 0x18), after.Length - 1];
    foreach (int at in changed.Distinct())
    {
        byte[] bad = after.ToArray(); bad[at] ^= 1;
        Refuse(() => plan.RequireFinal(bad), "full afterimage owner/complement corruption " + at);
        Refuse(() => plan.RevertExact(bad), "inverse corruption " + at);
    }
    byte[] prior = before.ToArray(); prior[^1] ^= 1; Refuse(() => plan.RequireCurrentBody(prior), "changed complete preimage");
    Refuse(() => plan.RequireCurrentBody(after), "cannot spend the same original allocation twice");
    Refuse(() => plan.RevertExact(after[..^1]), "truncated afterimage");
    Refuse(() => plan.RevertExact([.. after, 0]), "extra afterimage byte");
    Refuse(() => plan.CopyOwnerBytes((NativeJapaneseFontResidentRole)99), "unrecognized semantic owner");
    Refuse(() => plan.Layout.Map(new(plan.FinalDomain, 0, 4)), "wrong source revision");
    Refuse(() => plan.Layout.Map(new(plan.OriginalDomain, plan.OriginalModelOffset + plan.OriginalModelLength - 4, 8)), "crossing old model and shifted scene owner");
    byte[] returned = plan.CopyLevelData(); returned[0] ^= 1;
    Check(plan.CopyLevelData().SequenceEqual(after), "immutable plan output");
    returned = plan.CopyOwnerBytes(NativeJapaneseFontResidentRole.ImmutableAtlas); returned[0] ^= 1;
    Check(plan.CopyOwnerBytes(NativeJapaneseFontResidentRole.ImmutableAtlas).SequenceEqual(atlas), "immutable atlas output");
    Check(!plan.ModalPhaseLifetimeProven && !plan.CompleteRuntimeMemoryProven && !plan.VramInstallationApproved &&
        !plan.OriginalModelReferenceClosureProven && !plan.PublicationApproved, "all still-missing semantic/runtime/publication gates closed");
    Refuse(plan.RequireNativeInstallation, "allocation is not modal installation");
}
byte[] fixture = Fixture(), font = Enumerable.Range(0, 0xA000).Select(i => (byte)(i * 13 + 7)).ToArray();
var neutral = NativeJapaneseSignFontResidentPlan.Fixture(fixture, font); Verify(neutral, fixture, font);
Check(!neutral.SourceBound && !neutral.NormalLoadAddressProjectionSourceBound, "fixture cannot issue native authority");
var repeat = NativeJapaneseSignFontResidentPlan.Fixture(fixture, font);
Check(neutral.PlanSha256 == repeat.PlanSha256 && neutral.CopyLevelData().SequenceEqual(repeat.CopyLevelData()), "deterministic identical fixture allocation");
Refuse(() => neutral.Layout.Map(new(repeat.OriginalDomain, 0, 4)), "equal labels do not merge canonical source domains");
fixture[^1] ^= 1; font[0] ^= 1;
Check(neutral.CopyOriginalLevelData()[^1] != fixture[^1] && neutral.CopyOwnerBytes(NativeJapaneseFontResidentRole.ImmutableAtlas)[0] != font[0], "constructor snapshots both inputs");
Refuse(() => NativeJapaneseSignFontResidentPlan.Fixture(Fixture(), new byte[0x9FFF]), "short atlas");
Refuse(() => NativeJapaneseSignFontResidentPlan.Fixture(Fixture(), new byte[0xA001]), "extra atlas");
Refuse(() => NativeJapaneseSignFontResidentPlan.Fixture(new byte[0x2000], new byte[0xA000]), "short level");
Refuse(() => NativeJapaneseSignFontResidentPlan.Fixture(new byte[0x800800], new byte[0xA000]), "input ceiling");
Refuse(() => NativeJapaneseSignFontResidentPlan.Fixture(Fixture(), new byte[0xA000], 0x80187BB0 - 0x10000), "new owners exceed normal reserved floor");
Refuse(() => NativeJapaneseSignFontResidentPlan.Fixture(Fixture(), new byte[0xA000], 0x80085595), "unaligned RAM projection");
foreach (int field in new[] { 0, 0x10, 0x14, 0x18, 0x54, 0x1D0, 0x1000, 0x2088 })
{ byte[] bad = Fixture(); Put(bad, field, 0xFFFFFFFF); Refuse(() => NativeJapaneseSignFontResidentPlan.Fixture(bad, new byte[0xA000]), "malformed owner/grammar " + field); }
if (args.Length == 0)
{ Console.WriteLine($"JP_SIGN_FONT_RESIDENT_SOURCE_FREE_OK checks={checks} sourceReads=0 nativeApproved=false"); return; }
if (args.Length != 3 || args[0] != "--source") throw new ArgumentException("Usage: no arguments or --source <JP-BIN> <USA-BIN>");
using var jp = NativeRegionalSourceSession.Open(args[1], NativeDiscRevision.JapanScps10083);
using var usa = NativeRegionalSourceSession.Open(args[2], NativeDiscRevision.UsaScus94228);
var plan = NativeJapaneseSignFontResidentPlan.Capture(jp, usa);
Verify(plan, usa.ReadWadEntry(14), jp.ReadWadEntry(2));
Check(plan.SourceBound && plan.NormalLoadAddressProjectionSourceBound, "only actual capture issues source-backed address geometry");
Check(plan.Memory.CopyBuffer == 0x80085594 && plan.Memory.UsedLevelDataBytes == 0x38750 && plan.Memory.ModelStart == 0x800BDCE4,
    "exact untouched Dark Hollow baseline normal loader");
Check(plan.Memory.OriginalModelReadBytes == 0x6A800 && plan.Memory.FinalModelReadBytes == 0x7E800 && plan.Memory.SceneReadBytes == 0x11800,
    "native complete descriptor-read lengths");
Check(plan.Atlas.ModeledNormalRamStart == 0x801284E4 && plan.Backup.ModeledNormalRamStart == 0x801324E4 &&
    plan.Memory.FinalSceneStart == 0x8013C4E4 && plan.Memory.FinalSceneEnd == 0x8014DCE4,
    "independently computed two-owner address golden");
Check(plan.Memory.OriginalNormalGap == 0x4DECC && plan.Memory.FinalNormalGap == 0x39ECC, "resident charge is14000, not onlyA000");
Check(plan.CopyLevelData().Length == 0x2A1800 && plan.Registrations.Count == 35, "complete destination byte/registration census golden");
Refuse(() => NativeJapaneseSignFontResidentPlan.Capture(usa, jp), "wrong regional sessions");
jp.VerifyUnchanged(); usa.VerifyUnchanged();
Console.WriteLine($"JP_SIGN_FONT_RESIDENT_SOURCE_OK checks={checks} finalSha256={plan.FinalDataSha256} planSha256={plan.PlanSha256} normalGap={plan.Memory.FinalNormalGap:X} runtimeApproved=false candidateWrites=0");
